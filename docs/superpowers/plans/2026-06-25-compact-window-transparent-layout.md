# 图片翻译精简窗口透明布局 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让精简窗口透明无边框，截图贴回选区、按钮条作为悬浮额外内容，支持选区太窄横向延展、太靠下翻上方、上下都放不下则叠加。

**Architecture:** 重写 `ImageTranslateCompactWindowPlacement` 为返回完整布局结果的 `CreateLayout`（窗口矩形 + 图片偏移 + 按钮条位置 + 按钮侧枚举）。XAML 改为透明窗口 + 绝对定位容器，用布局结果定位图片与按钮条。保持 `ImageZoom` 渲染样式不变。

**Tech Stack:** WPF (.NET 10), xUnit, System.Drawing / System.Windows 坐标。

**Spec:** `docs/superpowers/specs/2026-06-25-compact-window-transparent-layout-design.md`

**测试命令（在 `src` 目录运行）：**
```bash
dotnet test Tests/STranslate.Tests/STranslate.Tests.csproj --filter "FullyQualifiedName~ImageTranslateCompactWindowPlacement"
```
**构建命令（在 `src` 目录运行）：**
```bash
dotnet build STranslate/STranslate.csproj
```

---

## 关键坐标约定（所有任务通用）

- 选区 `imageBounds` 是**物理像素** `System.Drawing.Rectangle`（来自 ScreenGrab `CaptureWithRegionAsync`）。
- 屏幕工作区 `workArea` 也是**物理像素** `System.Drawing.Rectangle`（从 `MonitorInfo.WorkingArea` DIP × DPI 换算，见 Task 5）。
- DPI 缩放 `dpiScaleX/dpiScaleY`（`DpiScale.DpiScaleX/Y`，1.0 = 100%）。
- 布局算法在**物理像素**域计算窗口矩形；输出给 XAML 的偏移/按钮位置换算成 DIP。
- `ToolbarReservedHeight = 64`（物理像素换算时 × DPI），按钮条 `Margin="8,6"` `Padding="8,6"`，故横向间距 `GapH = 8`，纵向间距 `GapV = 6`（DIP）。

## 文件结构

| 文件 | 责任 | 操作 |
| --- | --- | --- |
| `src/STranslate/Core/ImageTranslateCompactWindowPlacement.cs` | 布局算法：`CreateLayout` 返回完整布局结果 | 重写（保留 `CreateCenteredOnWorkArea`/`ToDipBounds`） |
| `src/Tests/STranslate.Tests/ImageTranslateCompactWindowPlacementTests.cs` | 布局算法单测 | 重写测试覆盖各场景 |
| `src/STranslate/Views/ImageTranslateCompactWindow.xaml` | 透明窗口 + 绝对定位图片与按钮条 | 修改 |
| `src/STranslate/Views/ImageTranslateCompactWindow.xaml.cs` | 调用 `CreateLayout`，把布局结果应用到 XAML | 修改 |

---

## Task 1: 新增布局结果类型与 `CreateLayout` 骨架（TDD - 居中场景）

**Files:**
- Modify: `src/STranslate/Core/ImageTranslateCompactWindowPlacement.cs`
- Test: `src/Tests/STranslate.Tests/ImageTranslateCompactWindowPlacementTests.cs`

- [ ] **Step 1: 写失败测试 — 居中场景（按钮条窄于选区，下方放得下）**

替换 `ImageTranslateCompactWindowPlacementTests.cs` 中 `CreateForImageBoundsUsesTargetDpiForToolbarHeight` 和 `CreateForImageBoundsKeepsTinyImageAboveMinimumPhysicalSize` 两个测试为新的 `CreateLayout` 测试。先加居中场景：

```csharp
[Fact]
public void CreateLayoutCentersToolbarWhenNarrowerThanImage()
{
    // 选区 640x360，100% 缩放，按钮条 DIP 高 64 宽 300。
    // workArea 足够大，下方放得下。
    var layout = ImageTranslateCompactWindowPlacement.CreateLayout(
        imageBounds: new Rectangle(100, 100, 640, 360),
        workArea: new Rectangle(0, 0, 1920, 1080),
        dpiScaleX: 1.0,
        dpiScaleY: 1.0,
        minWidthDip: 1,
        minImageHeightDip: 1,
        toolbarWidthDip: 300,
        toolbarHeightDip: 64,
        gapHDip: 8,
        gapVDip: 6,
        windowMarginDip: 8);

    // 窗口顶 = 选区顶；窗口高 = 图片高 + 纵向间距 + 按钮条高 + 底 margin
    Assert.Equal(new Rectangle(100, 100, 640, 360 + 6 + 64 + 8), layout.WindowBounds);
    // 图片在窗口内偏移 (0,0)，因为窗口顶左 = 选区顶左
    Assert.Equal(0, layout.ImageOffsetX);
    Assert.Equal(0, layout.ImageOffsetY);
    // 按钮条居中于选区：左 = 100 + (640-300)/2 = 270；下方
    // ToolbarBounds 是窗口内 DIP 偏移，故左 = (640-300)/2 = 170，顶 = 360+6 = 366
    Assert.Equal(170, layout.ToolbarX);
    Assert.Equal(366, layout.ToolbarY);
    Assert.Equal(ToolbarSide.Below, layout.ToolbarSide);
}
```

同时保留文件顶部其余测试（`CreateCenteredOnWorkAreaClampsToPhysicalWorkArea`、`ToDipBounds*`）不动。

- [ ] **Step 2: 运行测试验证失败**

```bash
dotnet test Tests/STranslate.Tests/STranslate.Tests.csproj --filter "FullyQualifiedName~ImageTranslateCompactWindowPlacement"
```
Expected: 编译失败（`CreateLayout` / `CompactWindowLayout` / `ToolbarSide` 未定义）。

- [ ] **Step 3: 实现布局结果类型与 `CreateLayout`（仅居中分支）**

在 `ImageTranslateCompactWindowPlacement.cs` 顶部新增枚举与结果类型，并实现 `CreateLayout` 的居中分支。**先删除旧 `CreateForImageBounds`**（它的两个测试已在 Step 1 删除；`ImageTranslateCompactWindow.xaml.cs` 的调用在 Task 6 改，此 Task 后构建会暂时失败，属预期，Task 6 修复）。

```csharp
using System.Drawing;
using System.Windows;

using WpfRect = System.Windows.Rect;

namespace STranslate.Core;

internal static class ImageTranslateCompactWindowPlacement
{
    internal static CompactWindowLayout CreateLayout(
        Rectangle imageBounds,
        Rectangle workArea,
        double dpiScaleX,
        double dpiScaleY,
        double minWidthDip,
        double minImageHeightDip,
        double toolbarWidthDip,
        double toolbarHeightDip,
        double gapHDip,
        double gapVDip,
        double windowMarginDip)
    {
        var toolbarWidth = ToPhysicalPixels(toolbarWidthDip, dpiScaleX);
        var toolbarHeight = ToPhysicalPixels(toolbarHeightDip, dpiScaleY);
        var gapH = ToPhysicalPixels(gapHDip, dpiScaleX);
        var gapV = ToPhysicalPixels(gapVDip, dpiScaleY);
        var margin = ToPhysicalPixels(windowMarginDip, dpiScaleY);
        var minWidth = ToPhysicalPixels(minWidthDip, dpiScaleX);
        var minImageHeight = ToPhysicalPixels(minImageHeightDip, dpiScaleY);

        var imageWidth = Math.Max(minWidth, imageBounds.Width);
        var imageHeight = Math.Max(minImageHeight, imageBounds.Height);

        // —— 横向 ——
        int windowLeft, windowRight, toolbarX;
        if (toolbarWidth <= imageWidth)
        {
            // 居中
            toolbarX = imageBounds.Left + (imageWidth - toolbarWidth) / 2;
            windowLeft = imageBounds.Left;
            windowRight = imageBounds.Left + imageWidth;
        }
        else
        {
            // 贴左缘向右延展；超出屏幕则镜像向左延展（Task 2 实现 else 分支）
            // Task 1 先抛 NotSupported，由 Task 2 填充
            throw new NotImplementedException("narrow image handled in Task 2");
        }

        // —— 纵向 ——（Task 1 只实现 Below 分支，其余 Task 3/4 实现）
        var spaceBelow = workArea.Bottom - (imageBounds.Top + imageHeight);
        if (spaceBelow < toolbarHeight + gapV)
            throw new NotImplementedException("vertical flip handled in Task 3/4");

        var imageBottom = imageBounds.Top + imageHeight;
        var toolbarY = imageBottom + gapV;
        var windowTop = imageBounds.Top;
        var windowBottom = imageBottom + gapV + toolbarHeight + margin;

        var windowWidth = windowRight - windowLeft;
        var windowHeight = windowBottom - windowTop;

        return new CompactWindowLayout(
            windowBounds: new Rectangle(windowLeft, windowTop, windowWidth, windowHeight),
            imageOffsetX: imageBounds.Left - windowLeft,
            imageOffsetY: imageBounds.Top - windowTop,
            toolbarX: toolbarX - windowLeft,
            toolbarY: toolbarY - windowTop,
            toolbarSide: ToolbarSide.Below);
    }

    // CreateCenteredOnWorkArea、ToDipBounds、ToPhysicalPixels、Clamp 保留不变（见原文件）
    internal static Rectangle CreateCenteredOnWorkArea(
        Rectangle workArea, System.Drawing.Size bitmapSize, double dpiScaleX, double dpiScaleY,
        double minWidthDip, double minImageHeightDip, double toolbarHeightDip,
        double maxWidthRatio, double maxHeightRatio)
    {
        // ... 原实现保持不变 ...
    }

    internal static WpfRect ToDipBounds(Rectangle physicalBounds, double dpiScaleX, double dpiScaleY) =>
        new(physicalBounds.Left / dpiScaleX, physicalBounds.Top / dpiScaleY,
            Math.Max(1d, physicalBounds.Width / dpiScaleX), Math.Max(1d, physicalBounds.Height / dpiScaleY));

    private static int ToPhysicalPixels(double dip, double dpiScale) =>
        Math.Max(1, (int)Math.Round(dip * dpiScale));

    private static int Clamp(int value, int min, int max) =>
        Math.Min(Math.Max(value, min), Math.Max(min, max));
}

internal enum ToolbarSide { Below, Above, Overlay }

internal sealed record CompactWindowLayout(
    Rectangle WindowBounds,
    int ImageOffsetX,
    int ImageOffsetY,
    int ToolbarX,
    int ToolbarY,
    ToolbarSide ToolbarSide);
```

> 注：`CreateCenteredOnWorkArea` 内部需保留完整原实现（不要真的写 `...`，复制原方法体）。`ToDipBounds` 也是。

- [ ] **Step 4: 运行测试验证通过**

```bash
dotnet test Tests/STranslate.Tests/STranslate.Tests.csproj --filter "FullyQualifiedName~ImageTranslateCompactWindowPlacement"
```
Expected: 居中测试 PASS；其余保留测试（`CreateCenteredOnWorkArea*`、`ToDipBounds*`）仍 PASS。

- [ ] **Step 5: 提交**

```bash
git add src/STranslate/Core/ImageTranslateCompactWindowPlacement.cs src/Tests/STranslate.Tests/ImageTranslateCompactWindowPlacementTests.cs
git commit -m "feat(placement): add CreateLayout with centered toolbar branch"
```

---

## Task 2: 横向延展分支（选区太窄 → 贴左缘，右边放不下 → 向左延展）

**Files:**
- Modify: `src/STranslate/Core/ImageTranslateCompactWindowPlacement.cs`
- Test: `src/Tests/STranslate.Tests/ImageTranslateCompactWindowPlacementTests.cs`

- [ ] **Step 1: 写失败测试 — 贴左缘向右延展**

加在居中测试之后：

```csharp
[Fact]
public void CreateLayoutExtendsRightWhenToolbarWiderThanImage()
{
    // 选区 200x200，按钮条宽 300 > 200。屏幕宽 1920，右边放得下。
    var layout = ImageTranslateCompactWindowPlacement.CreateLayout(
        imageBounds: new Rectangle(100, 100, 200, 200),
        workArea: new Rectangle(0, 0, 1920, 1080),
        dpiScaleX: 1.0, dpiScaleY: 1.0,
        minWidthDip: 1, minImageHeightDip: 1,
        toolbarWidthDip: 300, toolbarHeightDip: 64,
        gapHDip: 8, gapVDip: 6, windowMarginDip: 8);

    // 按钮条左缘 = 选区左 + gap = 108；窗口右 = max(选区右=300, 按钮条右=108+300=408) = 408
    // 窗口左 = 选区左 = 100；窗口宽 = 308
    Assert.Equal(new Rectangle(100, 100, 308, 200 + 6 + 64 + 8), layout.WindowBounds);
    Assert.Equal(0, layout.ImageOffsetX);   // 图片左=窗口左
    Assert.Equal(0, layout.ImageOffsetY);
    // 按钮条窗口内偏移：左 = 8 (gap)，顶 = 206
    Assert.Equal(8, layout.ToolbarX);
    Assert.Equal(206, layout.ToolbarY);
    Assert.Equal(ToolbarSide.Below, layout.ToolbarSide);
}

[Fact]
public void CreateLayoutExtendsLeftWhenRightEdgeExceedsWorkArea()
{
    // 选区左=1850 宽=50，按钮条宽 300。向右延展会顶出 workArea 右=1920。
    var layout = ImageTranslateCompactWindowPlacement.CreateLayout(
        imageBounds: new Rectangle(1850, 100, 50, 200),
        workArea: new Rectangle(0, 0, 1920, 1080),
        dpiScaleX: 1.0, dpiScaleY: 1.0,
        minWidthDip: 1, minImageHeightDip: 1,
        toolbarWidthDip: 300, toolbarHeightDip: 64,
        gapHDip: 8, gapVDip: 6, windowMarginDip: 8);

    // 向左延展：按钮条右缘 = 图片右 - gap = 1850+50-8 = 1892；按钮条左 = 1892-300 = 1592
    // 窗口左 = 按钮条左 - gap = 1584；窗口右 = 图片右 = 1900；窗口宽 = 316
    Assert.Equal(new Rectangle(1584, 100, 316, 200 + 6 + 64 + 8), layout.WindowBounds);
    // 图片在窗口内偏移：左 = 1850 - 1584 = 266
    Assert.Equal(266, layout.ImageOffsetX);
    Assert.Equal(0, layout.ImageOffsetY);
    // 按钮条窗口内偏移：左 = 1592 - 1584 = 8；顶 = 206
    Assert.Equal(8, layout.ToolbarX);
    Assert.Equal(206, layout.ToolbarY);
    Assert.Equal(ToolbarSide.Below, layout.ToolbarSide);
}
```

- [ ] **Step 2: 运行测试验证失败**

```bash
dotnet test Tests/STranslate.Tests/STranslate.Tests.csproj --filter "FullyQualifiedName~ImageTranslateCompactWindowPlacement"
```
Expected: 两个新测试 FAIL（`NotImplementedException`）。

- [ ] **Step 3: 实现横向延展 else 分支**

替换 Task 1 中 `else { throw new NotImplementedException... }` 为：

```csharp
        else
        {
            // 贴左缘向右延展
            var toolbarLeft = imageBounds.Left + gapH;
            var toolbarRight = toolbarLeft + toolbarWidth;
            var imageRight = imageBounds.Left + imageWidth;

            if (toolbarRight + gapH <= workArea.Right)
            {
                // 右边放得下：向右延展
                toolbarX = toolbarLeft;
                windowLeft = imageBounds.Left;
                windowRight = Math.Max(imageRight, toolbarRight + gapH);
            }
            else
            {
                // 右边放不下：镜像向左延展，按钮条右缘 = 图片右缘 - gap
                toolbarX = imageRight - gapH - toolbarWidth;
                windowLeft = toolbarX - gapH;
                windowRight = imageRight;
            }
        }
```

- [ ] **Step 4: 运行测试验证通过**

```bash
dotnet test Tests/STranslate.Tests/STranslate.Tests.csproj --filter "FullyQualifiedName~ImageTranslateCompactWindowPlacement"
```
Expected: 全部 PASS（含 Task 1 居中 + 本 Task 两个延展）。

- [ ] **Step 5: 提交**

```bash
git add src/STranslate/Core/ImageTranslateCompactWindowPlacement.cs src/Tests/STranslate.Tests/ImageTranslateCompactWindowPlacementTests.cs
git commit -m "feat(placement): extend right/left when toolbar wider than image"
```

---

## Task 3: 纵向翻上方分支（下方放不下、上方放得下）

**Files:**
- Modify: `src/STranslate/Core/ImageTranslateCompactWindowPlacement.cs`
- Test: `src/Tests/STranslate.Tests/ImageTranslateCompactWindowPlacementTests.cs`

- [ ] **Step 1: 写失败测试 — 翻上方**

```csharp
[Fact]
public void CreateLayoutFlipsAboveWhenBelowTooSmall()
{
    // 选区顶=950 高=100，底=1050。workArea 底=1080。下方空间=30 < 64+6=70 放不下。
    // 上方空间 = 950 - 0 = 950 >= 70 放得下 → 翻上方。
    var layout = ImageTranslateCompactWindowPlacement.CreateLayout(
        imageBounds: new Rectangle(100, 950, 640, 100),
        workArea: new Rectangle(0, 0, 1920, 1080),
        dpiScaleX: 1.0, dpiScaleY: 1.0,
        minWidthDip: 1, minImageHeightDip: 1,
        toolbarWidthDip: 300, toolbarHeightDip: 64,
        gapHDip: 8, gapVDip: 6, windowMarginDip: 8);

    // 翻上方：toolbarY = imageTop - gapV - toolbarHeight = 950 - 6 - 64 = 880
    // 窗口顶 = toolbarY = 880；窗口底 = imageBottom + margin = 1050 + 8 = 1058
    // 窗口高 = 1058 - 880 = 178
    Assert.Equal(new Rectangle(100, 880, 640, 178), layout.WindowBounds);
    // 图片窗口内偏移：左=0；顶 = imageTop - windowTop = 950 - 880 = 70
    Assert.Equal(0, layout.ImageOffsetX);
    Assert.Equal(70, layout.ImageOffsetY);
    // 按钮条居中：左 = (640-300)/2 = 170；顶 = 0
    Assert.Equal(170, layout.ToolbarX);
    Assert.Equal(0, layout.ToolbarY);
    Assert.Equal(ToolbarSide.Above, layout.ToolbarSide);
}
```

- [ ] **Step 2: 运行测试验证失败**

```bash
dotnet test Tests/STranslate.Tests/STranslate.Tests.csproj --filter "FullyQualifiedName~ImageTranslateCompactWindowPlacement"
```
Expected: FAIL（`NotImplementedException` from Task 1's vertical throw）。

- [ ] **Step 3: 实现纵向 Above 分支**

替换 Task 1 中纵向 `if (spaceBelow < toolbarHeight + gapV) throw new NotImplementedException(...)` 为：

```csharp
        var imageBottom = imageBounds.Top + imageHeight;
        int toolbarY, windowTop, windowBottom;
        ToolbarSide side;

        var spaceBelow = workArea.Bottom - imageBottom;
        if (spaceBelow >= toolbarHeight + gapV)
        {
            // 下方放得下
            toolbarY = imageBottom + gapV;
            windowTop = imageBounds.Top;
            windowBottom = imageBottom + gapV + toolbarHeight + margin;
            side = ToolbarSide.Below;
        }
        else
        {
            var spaceAbove = imageBounds.Top - workArea.Top;
            if (spaceAbove >= toolbarHeight + gapV)
            {
                // 翻上方
                toolbarY = imageBounds.Top - gapV - toolbarHeight;
                windowTop = toolbarY;
                windowBottom = imageBottom + margin;
                side = ToolbarSide.Above;
            }
            else
            {
                throw new NotImplementedException("overlay handled in Task 4");
            }
        }

        var windowWidth = windowRight - windowLeft;
        var windowHeight = windowBottom - windowTop;

        return new CompactWindowLayout(
            windowBounds: new Rectangle(windowLeft, windowTop, windowWidth, windowHeight),
            imageOffsetX: imageBounds.Left - windowLeft,
            imageOffsetY: imageBounds.Top - windowTop,
            toolbarX: toolbarX - windowLeft,
            toolbarY: toolbarY - windowTop,
            toolbarSide: side);
```

> 同时删除 Task 1 里纵向部分原有的 `imageBottom`/`toolbarY`/`windowTop`/`windowBottom` 旧赋值，避免重复定义。

- [ ] **Step 4: 运行测试验证通过**

```bash
dotnet test Tests/STranslate.Tests/STranslate.Tests.csproj --filter "FullyQualifiedName~ImageTranslateCompactWindowPlacement"
```
Expected: 全部 PASS（居中 + 横向延展 + 翻上方）。

- [ ] **Step 5: 提交**

```bash
git add src/STranslate/Core/ImageTranslateCompactWindowPlacement.cs src/Tests/STranslate.Tests/ImageTranslateCompactWindowPlacementTests.cs
git commit -m "feat(placement): flip toolbar above when below too small"
```

---

## Task 4: 叠加分支（上下都放不下）

**Files:**
- Modify: `src/STranslate/Core/ImageTranslateCompactWindowPlacement.cs`
- Test: `src/Tests/STranslate.Tests/ImageTranslateCompactWindowPlacementTests.cs`

- [ ] **Step 1: 写失败测试 — 叠加**

```csharp
[Fact]
public void CreateLayoutOverlaysWhenBothSidesTooSmall()
{
    // 选区占满高度：顶=0 高=1080=workArea 高。下方空间=0，上方空间=0，都 < 70 → 叠加。
    var layout = ImageTranslateCompactWindowPlacement.CreateLayout(
        imageBounds: new Rectangle(100, 0, 640, 1080),
        workArea: new Rectangle(0, 0, 1920, 1080),
        dpiScaleX: 1.0, dpiScaleY: 1.0,
        minWidthDip: 1, minImageHeightDip: 1,
        toolbarWidthDip: 300, toolbarHeightDip: 64,
        gapHDip: 8, gapVDip: 6, windowMarginDip: 8);

    // 叠加：toolbarY = imageBottom - toolbarHeight = 1080 - 64 = 1016
    // 窗口顶 = imageTop = 0；窗口底 = imageBottom = 1080；窗口高 = 1080
    Assert.Equal(new Rectangle(100, 0, 640, 1080), layout.WindowBounds);
    Assert.Equal(0, layout.ImageOffsetX);
    Assert.Equal(0, layout.ImageOffsetY);
    // 按钮条居中：左 = (640-300)/2 = 170；顶 = 1016
    Assert.Equal(170, layout.ToolbarX);
    Assert.Equal(1016, layout.ToolbarY);
    Assert.Equal(ToolbarSide.Overlay, layout.ToolbarSide);
}
```

- [ ] **Step 2: 运行测试验证失败**

```bash
dotnet test Tests/STranslate.Tests/STranslate.Tests.csproj --filter "FullyQualifiedName~ImageTranslateCompactWindowPlacement"
```
Expected: FAIL（`NotImplementedException` from Task 3's overlay throw）。

- [ ] **Step 3: 实现叠加分支**

替换 Task 3 中 `else { throw new NotImplementedException("overlay..."); }` 为：

```csharp
            else
            {
                // 上下都放不下：叠加在图片底部之上
                toolbarY = imageBottom - toolbarHeight;
                windowTop = imageBounds.Top;
                windowBottom = imageBottom;
                side = ToolbarSide.Overlay;
            }
```

- [ ] **Step 4: 运行测试验证通过**

```bash
dotnet test Tests/STranslate.Tests/STranslate.Tests.csproj --filter "FullyQualifiedName~ImageTranslateCompactWindowPlacement"
```
Expected: 全部 PASS（6 个 CreateLayout 测试 + 保留的 `CreateCenteredOnWorkArea*`、`ToDipBounds*`）。

- [ ] **Step 5: 提交**

```bash
git add src/STranslate/Core/ImageTranslateCompactWindowPlacement.cs src/Tests/STranslate.Tests/ImageTranslateCompactWindowPlacementTests.cs
git commit -m "feat(placement): overlay toolbar when both sides too small"
```

---

## Task 5: 获取选区所在屏幕的物理工作区辅助方法

**Files:**
- Modify: `src/STranslate/Views/ImageTranslateCompactWindow.xaml.cs`

布局算法需要选区所在屏幕的**物理像素**工作区。`MonitorInfo.WorkingArea` 是 DIP，需 × DPI 换算。

- [ ] **Step 1: 在 `ImageTranslateCompactWindow.xaml.cs` 新增私有方法**

加在 `GetDpiScale` 静态方法之后：

```csharp
    /// <summary>
    /// 获取包含指定物理坐标的屏幕工作区（物理像素）。
    /// MonitorInfo.WorkingArea 是 DIP，按该屏幕 DPI 换算回物理像素。
    /// </summary>
    private static DrawingRectangle GetPhysicalWorkArea(int physicalX, int physicalY)
    {
        var point = new DrawingPoint(physicalX, physicalY);
        var monitor = MonitorInfo.GetDisplayMonitors()
            .FirstOrDefault(m =>
            {
                var b = m.Bounds;
                return physicalX >= b.X && physicalX < b.X + b.Width
                    && physicalY >= b.Y && physicalY < b.Y + b.Height;
            }) ?? MonitorInfo.GetPrimaryDisplayMonitor();

        var dpi = Win32Helper.GetDpiScaleForPhysicalPoint(physicalX, physicalY);
        return new DrawingRectangle(
            (int)Math.Round(monitor.WorkingArea.X * dpi.DpiScaleX),
            (int)Math.Round(monitor.WorkingArea.Y * dpi.DpiScaleY),
            (int)Math.Round(monitor.WorkingArea.Width * dpi.DpiScaleX),
            (int)Math.Round(monitor.WorkingArea.Height * dpi.DpiScaleY));
    }
```

- [ ] **Step 2: 验证编译**

```bash
dotnet build STranslate/STranslate.csproj
```
Expected: BUILD SUCCEEDED（方法暂未被调用，但须编译通过）。

- [ ] **Step 3: 提交**

```bash
git add src/STranslate/Views/ImageTranslateCompactWindow.xaml.cs
git commit -m "feat(compact): add physical work area helper"
```

---

## Task 6: 改造 `PlaceOnPhysicalBounds` 调用 `CreateLayout` 并应用布局到 XAML 字段

**Files:**
- Modify: `src/STranslate/Views/ImageTranslateCompactWindow.xaml.cs`

- [ ] **Step 1: 新增布局结果字段与 `ApplyLayout` 方法**

在类字段区（`_isContextMenuOpen` 附近）新增：

```csharp
    private CompactWindowLayout? _layout;
```

新增常量（替换/补充原有 `ToolbarReservedHeight`）：

```csharp
    private const double ToolbarWidth = 300;   // 按钮条约 7 个 32px 按钮 + spacing + padding
    private const double GapH = 8;
    private const double GapV = 6;
    private const double WindowMargin = 8;
```

> `ToolbarWidth` 需实测微调；7 个 IconButton(32) + 6 个 spacing(5) + padding(8*2) + margin(8*2) ≈ 300。实现时如按钮条实际渲染宽度不同，按实际测量值校准此常量。

- [ ] **Step 2: 重写 `PlaceOnPhysicalBounds` 调用 `CreateLayout`**

替换原 `PlaceOnPhysicalBounds` 方法体：

```csharp
    private void PlaceOnPhysicalBounds(DrawingRectangle bounds)
    {
        var dpiScale = GetDpiScale(bounds);
        var workArea = GetPhysicalWorkArea(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);

        _layout = ImageTranslateCompactWindowPlacement.CreateLayout(
            imageBounds: bounds,
            workArea: workArea,
            dpiScaleX: dpiScale.DpiScaleX,
            dpiScaleY: dpiScale.DpiScaleY,
            minWidthDip: MinWidth,
            minImageHeightDip: MinHeight,
            toolbarWidthDip: ToolbarWidth,
            toolbarHeightDip: ToolbarReservedHeight,
            gapHDip: GapH,
            gapVDip: GapV,
            windowMarginDip: WindowMargin);

        PlaceOnPhysicalWindowBounds(_layout.WindowBounds, dpiScale);
        ApplyLayoutToVisualTree();
    }
```

- [ ] **Step 3: 扩展 `CompactWindowLayout` 携带图片与按钮条显示尺寸**

`ApplyLayoutToVisualTree` 需要图片显示尺寸才能给 `ImageZoom` 设固定 `Width/Height`（钉位置 + 1:1）。当前 record 只有窗口矩形和偏移，无法从 `WindowBounds` 反推选区尺寸。故先扩展 record。

回到 `ImageTranslateCompactWindowPlacement.cs`，修改 record：

```csharp
internal sealed record CompactWindowLayout(
    Rectangle WindowBounds,
    int ImageOffsetX,
    int ImageOffsetY,
    int ImageWidth,        // 新增：图片显示物理宽
    int ImageHeight,       // 新增：图片显示物理高
    int ToolbarX,
    int ToolbarY,
    int ToolbarWidth,      // 新增：按钮条物理宽
    int ToolbarHeight,     // 新增：按钮条物理高
    ToolbarSide ToolbarSide);
```

`CreateLayout` 返回处补充 `imageWidth`/`imageHeight`/`toolbarWidth`/`toolbarHeight`（这些局部变量已在方法体内存在）：

```csharp
        return new CompactWindowLayout(
            windowBounds: new Rectangle(windowLeft, windowTop, windowWidth, windowHeight),
            imageOffsetX: imageBounds.Left - windowLeft,
            imageOffsetY: imageBounds.Top - windowTop,
            imageWidth: imageWidth,
            imageHeight: imageHeight,
            toolbarX: toolbarX - windowLeft,
            toolbarY: toolbarY - windowTop,
            toolbarWidth: toolbarWidth,
            toolbarHeight: toolbarHeight,
            toolbarSide: side);
```

> 测试不直接 `new CompactWindowLayout(...)`（只断言返回属性），record 新增必填字段不影响已有断言。

- [ ] **Step 4: 验证算法测试仍通过**

```bash
dotnet test Tests/STranslate.Tests/STranslate.Tests.csproj --filter "FullyQualifiedName~ImageTranslateCompactWindowPlacement"
```
Expected: 全部 PASS（record 新增字段不影响断言）。

- [ ] **Step 5: 新增 `ApplyLayoutToVisualTree` 实现**

在 `PlaceOnPhysicalWindowBounds` 之后新增。把布局结果换算成 DIP，给 `ImageZoom` 与按钮条 `Border` 设固定尺寸 + 左上对齐 + Margin 偏移。`PART_ToolbarBorder` 的 `x:Name` 在 Task 7 的 XAML 中定义（此 Task 编译会因缺该 name 失败，Task 7 修复）。

```csharp
    /// <summary>
    /// 把布局结果换算成 DIP 并应用到 ImageZoom 与按钮条 Border：
    /// 固定 Width/Height（保证图片 1:1 显示）、左上对齐、Margin 定位到选区绝对位置。
    /// 按钮条 ZIndex 高于图片（XAML 内 PART_ToolbarBorder Panel.ZIndex=20）。
    /// </summary>
    private void ApplyLayoutToVisualTree()
    {
        if (_layout is null) return;
        var dpi = GetDpiScale(_layout.WindowBounds);
        var sx = dpi.DpiScaleX;
        var sy = dpi.DpiScaleY;

        // 图片：固定尺寸 + 左上对齐 + Margin 偏移（钉在选区绝对位置）
        PART_ImageZoom.HorizontalAlignment = HorizontalAlignment.Left;
        PART_ImageZoom.VerticalAlignment = VerticalAlignment.Top;
        PART_ImageZoom.Width = _layout.ImageWidth / sx;
        PART_ImageZoom.Height = _layout.ImageHeight / sy;
        PART_ImageZoom.Margin = new Thickness(_layout.ImageOffsetX / sx, _layout.ImageOffsetY / sy, 0, 0);

        // 按钮条：固定尺寸 + 左上对齐 + Margin 偏移，ZIndex 高于图片
        PART_ToolbarBorder.HorizontalAlignment = HorizontalAlignment.Left;
        PART_ToolbarBorder.VerticalAlignment = VerticalAlignment.Top;
        PART_ToolbarBorder.Width = _layout.ToolbarWidth / sx;
        PART_ToolbarBorder.Height = _layout.ToolbarHeight / sy;
        PART_ToolbarBorder.Margin = new Thickness(_layout.ToolbarX / sx, _layout.ToolbarY / sy, 0, 0);
    }
```

> `GetDpiScale(_layout.WindowBounds)` 接受 `DrawingRectangle`，OK（`CompactWindowLayout.WindowBounds` 是 `System.Drawing.Rectangle`）。

- [ ] **Step 6: 提交（主项目此时编译失败，因缺 `PART_ToolbarBorder`；仅提交算法 + code-behind，Task 7 修复编译）**

```bash
git add src/STranslate/Core/ImageTranslateCompactWindowPlacement.cs src/STranslate/Views/ImageTranslateCompactWindow.xaml.cs
git commit -m "feat(compact): apply CreateLayout result to visual tree"
```

> 若团队规范要求每次提交可构建，可把 Task 6 与 Task 7 合并提交。此处分开是为了 TDD 节奏。

---

## Task 7: XAML 透明窗口 + 绝对定位容器

**Files:**
- Modify: `src/STranslate/Views/ImageTranslateCompactWindow.xaml`

- [ ] **Step 1: 修改窗口属性为透明**

修改 `Window` 根元素属性：
- 删除 `Background="{DynamicResource ApplicationPageBackgroundThemeBrush}"`，改为 `Background="Transparent"`。
- 新增 `AllowsTransparency="True"`。
- 保留 `WindowStyle="None"`、`ResizeMode="NoResize"`、`Topmost="True"`、`WindowStartupLocation="Manual"`、`ShowInTaskbar="False"`。

> ⚠️ **透明渲染风险**（spec 已记录）：`AllowsTransparency=True` 切换软件渲染，可能影响 `ImageZoom` 渲染质量。实现后必须实测对比图片清晰度。若明显下降，回退讨论改用 `WindowChrome`/`HwndSource` 透明方案。

- [ ] **Step 2: 把根 Grid 改为 Canvas 绝对定位容器**

将 `<Grid Background="Transparent">` 及其 `RowDefinitions` 替换为透明 `Canvas`（或保留 `Grid` 但去掉 RowDefinitions，子元素用 Margin 绝对定位）。推荐用 `Grid`（无 RowDefinitions）+ 子元素 `HorizontalAlignment=Left/VerticalAlignment=Top` + Margin，因为 code-behind 已按此实现：

```xml
    <Grid Background="Transparent">
        <!-- 图片区：由 code-behind 设置 Width/Height/Margin/Alignment -->
        <control:ImageZoom
            x:Name="PART_ImageZoom"
            AlwaysHideZoomValueHint="True"
            DisableDoubleClickReset="True"
            IsPanAndZoomEnabled="False"
            MaxZoomRatio="5.0"
            MinZoomRatio="0.1"
            OcrWords="{Binding OcrWords}"
            Source="{Binding DisplayImage}">
            <!-- 保留原 InputBindings、Resources、ContextMenu（原 76-128 行）  -->
            <control:ImageZoom.InputBindings>
                <!-- ...原内容不变... -->
            </control:ImageZoom.InputBindings>
            <control:ImageZoom.Resources>
                <!-- ...原内容不变... -->
            </control:ImageZoom.Resources>
            <control:ImageZoom.ContextMenu>
                <!-- ...原内容不变... -->
            </control:ImageZoom.ContextMenu>
        </control:ImageZoom>

        <!-- 无位置信息提示：保持 Grid.Row=0 → 改为放在 ImageZoom 同层，Panel.ZIndex 高 -->
        <ui:InfoBar
            MaxWidth="480"
            Margin="12"
            HorizontalAlignment="Center"
            VerticalAlignment="Top"
            Panel.ZIndex="10"
            IsOpen="{Binding IsNoLocationInfoVisible, Mode=TwoWay}"
            Title="{DynamicResource NoLocationInfoTitle}"
            Message="{DynamicResource NoLocationInfoMessageForImTrans}"
            Severity="Warning" />

        <!-- 按钮条：由 code-behind 设置 Width/Height/Margin/Alignment，ZIndex 高于图片 -->
        <Border
            x:Name="PART_ToolbarBorder"
            Margin="8,6"
            Padding="8,6"
            Panel.ZIndex="20"
            Background="#CC1F1F1F"
            CornerRadius="22">
            <ikw:SimpleStackPanel Orientation="Horizontal" Spacing="5">
                <!-- 保留原 7 个 IconButton（原 153-191 行），内容不变 -->
            </ikw:SimpleStackPanel>
        </Border>

        <!-- 执行遮罩：Grid.RowSpan=2 → 改为覆盖整个 Grid -->
        <Border
            Background="#B0000000"
            IsHitTestVisible="{Binding IsExecuting}"
            Visibility="{Binding IsExecuting, Converter={cnvt:BoolToVisibilityConverter}}"
            d:Visibility="Collapsed">
            <!-- 保留原 ProgressRing + TextBlock（原 201-218 行） -->
        </Border>
    </Grid>
```

> 关键：保留所有 IconButton、ContextMenu、InputBindings、InfoBar、遮罩的**原始内容**，只改容器结构与定位属性。`PART_ToolbarBorder` 移除原 `HorizontalAlignment="Center" VerticalAlignment="Center"`（改由 code-behind 设 `Left/Top`）。移除原 `Grid.Row`/`Grid.RowSpan` 引用。

- [ ] **Step 3: 构建验证**

```bash
dotnet build STranslate/STranslate.csproj
```
Expected: BUILD SUCCEEDED。若 `AllowsTransparency` 与某些属性冲突报错（如 `ResizeMode` 在透明窗口下限制），按编译器提示调整。

- [ ] **Step 4: 提交**

```bash
git add src/STranslate/Views/ImageTranslateCompactWindow.xaml
git commit -m "feat(compact): transparent window with absolute-positioned image and toolbar"
```

---

## Task 8: 实测校准按钮条宽度常量 + 透明渲染验证

**Files:**
- Possibly modify: `src/STranslate/Views/ImageTranslateCompactWindow.xaml.cs`（`ToolbarWidth` 常量）

此 Task 是人工验证，无单测。

- [ ] **Step 1: 运行应用，触发图片翻译精简窗口**

在 `src` 目录：
```bash
dotnet build STranslate/STranslate.csproj
```
然后运行 STranslate，配置图片翻译窗口模式为 Compact，截图翻译。

- [ ] **Step 2: 校准 `ToolbarWidth`**

观察按钮条实际渲染宽度。若与 `ToolbarWidth = 300` 偏差明显（导致居中或延展判定不准），用实际像素值（÷ DPI 得 DIP）更新常量。校准后重新构建。

- [ ] **Step 3: 验证透明渲染质量**

对比 `AllowsTransparency=True` 前后图片清晰度。确认 spec 的透明渲染风险未触发（图片无模糊/锯齿）。若质量下降，记录现象，回退讨论 `WindowChrome` 方案。

- [ ] **Step 4: 跑全部场景（spec 验证清单 1-7）**

逐一验证：
1. 中部框选 → 图片贴选区，按钮条下方居中，无窗口背景。
2. 窄竖框选 → 图片钉左缘，窗口向右延展，按钮条左对齐。
3. 靠右窄框选 → 按钮条向左延展，窗口左扩，图片不动。
4. 贴底框选 → 按钮条翻上方，窗口顶上移。
5. 满屏高度框选 → 按钮条压图片底部之上，可见可点。
6. 多显示器 + 非 100% → 贴图零偏移。
7. 浅色截图浅色桌面 → 无背景但按钮条清晰。

- [ ] **Step 5: 提交校准（如有改动）**

```bash
git add src/STranslate/Views/ImageTranslateCompactWindow.xaml.cs
git commit -m "fix(compact): calibrate toolbar width constant"
```

若无改动，跳过提交。

---

## Task 9: 更新流程文档

**Files:**
- Modify: `src/docs/flow-image-translation.md`

- [ ] **Step 1: 更新「窗口模式」与「常见改动任务」章节**

在 `flow-image-translation.md` 的「窗口模式」章节，将 Compact 描述更新为：

```markdown
- `Compact` 使用无标题、不可缩放、非任务栏、**完全透明**窗口，窗口本身无背景色；屏幕上只看到截图内容 + 悬浮按钮条（按钮条自带半透明胶囊背景）。
- 图片始终钉在截图选区的物理屏幕位置（贴图位置不变铁律）；按钮条作为悬浮额外内容，根据空间自动选择位置：
  - 横向：按钮条窄于选区时居中；宽于选区时贴左缘向右延展，右边放不下则镜像向左延展。
  - 纵向：默认在图片下方；下方放不下翻上方；上下都放不下则叠加在图片底部之上（按钮条 ZIndex 高于图片）。
- 布局算法在 `ImageTranslateCompactWindowPlacement.CreateLayout`，返回窗口矩形、图片偏移、按钮条位置与 `ToolbarSide`（`Below`/`Above`/`Overlay`）。
```

在「常见改动任务」末尾追加：

```markdown
- 调整精简窗口布局/定位/按钮条翻向逻辑：改 `ImageTranslateCompactWindowPlacement.CreateLayout`，并补 `ImageTranslateCompactWindowPlacementTests` 对应场景。
```

- [ ] **Step 2: 提交**

```bash
git add src/docs/flow-image-translation.md
git commit -m "docs: update image translation flow for transparent compact window"
```

---

## Self-Review

**1. Spec coverage:**
- 决策1 透明无边框 → Task 7 ✓
- 决策2 贴左缘延展 → Task 2 ✓
- 决策3 向左延展 → Task 2 ✓
- 决策4 翻上方 → Task 3 ✓
- 决策5 叠加 → Task 4 ✓
- 两条铁律（贴图位置不变 + 按钮可见可点）→ 算法图片偏移始终 = imageBounds - windowLeft/Top（Task 1-4），叠加分支按钮 ZIndex 高（Task 7）✓
- 不动 ImageZoom 渲染样式 → Task 7 保留 Viewbox/Image/Stretch ✓
- 透明渲染风险实测 → Task 8 ✓
- 测试覆盖各场景 → Task 1-4 ✓
- 工作区物理换算 → Task 5 ✓

**2. Placeholder scan:** 无 TBD/TODO。Task 1 的 `CreateCenteredOnWorkArea` 标注"保留原实现"——这是要求保留现有代码，非占位；执行时须复制原方法体。已明确说明。

**3. Type consistency:** `CompactWindowLayout` 字段在 Task 6 Step 4 扩展后，Task 1-4 测试不直接构造 record（只断言返回属性），不受影响 ✓。`ToolbarSide` 枚举 `Below/Above/Overlay` 在 Task 1 定义、Task 3/4 使用一致 ✓。`CreateLayout` 参数签名 Task 1-8 一致 ✓。`PART_ToolbarBorder` name 在 Task 6（code-behind）与 Task 7（XAML）一致 ✓。
