# GoogleOCR 内置 OCR 插件 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 1.0 的 GoogleOCR 适配为 v2.0 内置 OCR 插件 `STranslate.Plugin.Ocr.Google`，调用 Google Vision TEXT_DETECTION 返回词级文本+坐标框。

**Architecture:** 仿 Baidu 内置插件（扁平目录、`ProjectReference` 引用插件框架、`plugin.json`+`icon.png`+`Languages`+`Main`+`Settings`+`View`+`ViewModel`）。`Main` 实现 `IOcrPlugin`，用 `IPluginContext.HttpService.PostAsync` 发请求、`System.Text.Json.Nodes.JsonNode` 解析响应。

**Tech Stack:** .NET 10.0-windows、WPF、CommunityToolkit.Mvvm、System.Text.Json、iNKORE.UI.WPF.Modern。

**参考文件（只读，用于对齐模式）：**
- `src/Plugins/STranslate.Plugin.Ocr.Baidu/`（整体骨架、csproj、plugin.json、Settings/View/ViewModel/Languages 模式）
- `src/Plugins/ThirdPlugins/STranslate.Plugin.Ocr.Gemini/STranslate.Plugin.Ocr.Gemini/Main.cs`（JsonNode 解析 + UriBuilder 模式）
- `src/STranslate.Plugin/IOcrPlugin.cs`（接口、OcrResult/OcrContent/BoxPoint 定义）
- `src/STranslate.Plugin/IHttpService.cs`（PostAsync 签名）
- `src/STranslate/Core/Constant.cs`（PrePluginIDs）

**关于测试：** 当前仓库无插件单元测试先例——测试项目 `STranslate.Tests` 仅引用主项目 `STranslate.csproj`，不引用任何插件项目；插件核心逻辑依赖运行时注入的 `IPluginContext.HttpService`，无现成 mock 基础设施。本计划沿用既有插件约定，以**编译验证**作为主要验证手段（与 Baidu/Gemini 等插件一致），不为单独的插件项目强加不匹配的 TDD。

---

## File Structure

创建目录 `src/Plugins/STranslate.Plugin.Ocr.Google/`，含：

| 文件 | 职责 |
|---|---|
| `STranslate.Plugin.Ocr.Google.csproj` | 项目文件，仿 Baidu |
| `Main.cs` | `IOcrPlugin` 实现：URL 构造、请求、JsonNode 解析、坐标转换 |
| `Settings.cs` | 持久化配置 `Url`/`ApiKey` |
| `plugin.json` | 插件元数据，PluginID=`2e83ee2f5dbf45249a3bd1457a326abf` |
| `icon.png` | 图标（二进制复制 Baidu 的 icon.png 占位） |
| `ViewModel/SettingsViewModel.cs` | MVVM 绑定 + 自动保存 |
| `View/SettingsView.xaml` | 设置 UI（Url/ApiKey/官网） |
| `View/SettingsView.xaml.cs` | code-behind |
| `Languages/en.xaml`+`en.json` | 英文资源 |
| `Languages/zh-cn.xaml`+`zh-cn.json` | 简中资源 |
| `Languages/zh-tw.xaml`+`zh-tw.json` | 繁中资源 |
| `Languages/ja.xaml`+`ja.json` | 日文资源 |
| `Languages/ko.xaml`+`ko.json` | 韩文资源 |

修改 2 个既有文件：
- `src/STranslate.slnx`（`/Plugins/` OCR 区段追加项目引用）
- `src/STranslate/Core/Constant.cs`（`PrePluginIDs` 追加 ID）

---

## Task 1: 创建项目骨架（csproj + plugin.json + icon）

**Files:**
- Create: `src/Plugins/STranslate.Plugin.Ocr.Google/STranslate.Plugin.Ocr.Google.csproj`
- Create: `src/Plugins/STranslate.Plugin.Ocr.Google/plugin.json`
- Copy: `src/Plugins/STranslate.Plugin.Ocr.Baidu/icon.png` → `src/Plugins/STranslate.Plugin.Ocr.Google/icon.png`

- [ ] **Step 1: 创建 csproj**

Create `src/Plugins/STranslate.Plugin.Ocr.Google/STranslate.Plugin.Ocr.Google.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0-windows</TargetFramework>
        <UseWPF>true</UseWPF>
        <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
        <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
        <!--// 编译后打包为插件 //-->
        <!--<EnableAutoPackage>true</EnableAutoPackage>-->
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
        <DebugSymbols>true</DebugSymbols>
        <DebugType>portable</DebugType>
        <Optimize>false</Optimize>
        <OutputPath>..\..\.artifacts\Debug\Plugins\STranslate.Plugin.Ocr.Google\</OutputPath>
        <DefineConstants>DEBUG;TRACE</DefineConstants>
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>4</WarningLevel>
        <Prefer32Bit>false</Prefer32Bit>
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
        <DebugType>none</DebugType>
        <Optimize>true</Optimize>
        <OutputPath>..\..\.artifacts\Release\Plugins\STranslate.Plugin.Ocr.Google\</OutputPath>
        <DefineConstants>TRACE</DefineConstants>
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>4</WarningLevel>
        <Prefer32Bit>false</Prefer32Bit>
    </PropertyGroup>

    <ItemGroup>
        <Content Include="Languages\*.*">
            <Generator>MSBuild:Compile</Generator>
            <SubType>Designer</SubType>
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </Content>
        <Content Include="icon.png">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </Content>
        <Content Include="plugin.json">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </Content>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\STranslate.Plugin\STranslate.Plugin.csproj" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: 创建 plugin.json**

Create `src/Plugins/STranslate.Plugin.Ocr.Google/plugin.json`:

```json
{
  "PluginID": "2e83ee2f5dbf45249a3bd1457a326abf",
  "Name": "Google OCR",
  "Description": "Google Vision OCR plugin for STranslate",
  "Author": "zggsong",
  "Version": "1.0.0",
  "Website": "https://github.com/STranslate/STranslate",
  "ExecuteFileName": "STranslate.Plugin.Ocr.Google.dll",
  "IconPath": "icon.png"
}
```

- [ ] **Step 3: 复制 icon.png**

Run:
```bash
cp src/Plugins/STranslate.Plugin.Ocr.Baidu/icon.png src/Plugins/STranslate.Plugin.Ocr.Google/icon.png
```

- [ ] **Step 4: 登记到解决方案**

Modify `src/STranslate.slnx`，在 OCR 插件区段（`STranslate.Plugin.Ocr.WeChatBuiltIn` 那行之后）追加：

```xml
    <Project Path="Plugins/STranslate.Plugin.Ocr.Google/STranslate.Plugin.Ocr.Google.csproj" />
```

完整区段应为：
```xml
    <!-- OCR 插件 -->
    <Project Path="Plugins/STranslate.Plugin.Ocr.Baidu/STranslate.Plugin.Ocr.Baidu.csproj" />
    <Project Path="Plugins/STranslate.Plugin.Ocr.OpenAI/STranslate.Plugin.Ocr.OpenAI.csproj" />
    <Project Path="Plugins/STranslate.Plugin.Ocr.WeChatBuiltIn/STranslate.Plugin.Ocr.WeChatBuiltIn.csproj" />
    <Project Path="Plugins/STranslate.Plugin.Ocr.Google/STranslate.Plugin.Ocr.Google.csproj" />
```

- [ ] **Step 5: 提交**

```bash
git add src/Plugins/STranslate.Plugin.Ocr.Google/STranslate.Plugin.Ocr.Google.csproj src/Plugins/STranslate.Plugin.Ocr.Google/plugin.json src/Plugins/STranslate.Plugin.Ocr.Google/icon.png src/STranslate.slnx
git commit -m "feat(ocr): scaffold GoogleOCR plugin project"
```

---

## Task 2: Settings 模型

**Files:**
- Create: `src/Plugins/STranslate.Plugin.Ocr.Google/Settings.cs`

- [ ] **Step 1: 创建 Settings.cs**

Create `src/Plugins/STranslate.Plugin.Ocr.Google/Settings.cs`:

```csharp
namespace STranslate.Plugin.Ocr.Google;

public class Settings
{
    public string Url { get; set; } = "https://vision.googleapis.com/";
    public string ApiKey { get; set; } = string.Empty;
}
```

- [ ] **Step 2: 提交**

```bash
git add src/Plugins/STranslate.Plugin.Ocr.Google/Settings.cs
git commit -m "feat(ocr): add GoogleOCR Settings model"
```

---

## Task 3: Main 插件实现

**Files:**
- Create: `src/Plugins/STranslate.Plugin.Ocr.Google/Main.cs`

- [ ] **Step 1: 创建 Main.cs**

Create `src/Plugins/STranslate.Plugin.Ocr.Google/Main.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using STranslate.Plugin.Ocr.Google.View;
using STranslate.Plugin.Ocr.Google.ViewModel;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace STranslate.Plugin.Ocr.Google;

public class Main : ObservableObject, IOcrPlugin
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public IEnumerable<LangEnum> SupportedLanguages => Enum.GetValues<LangEnum>();

    public bool SupportBoxPoints() => true;

    public Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public void Dispose() => _viewModel?.Dispose();

    public string? GetLanguage(LangEnum langEnum) => null;

    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default)
    {
        // 构造请求 URL: {Url}/v1/images:annotate?key={ApiKey}
        var uriBuilder = new UriBuilder(Settings.Url)
        {
            Path = "/v1/images:annotate",
            Query = $"key={Settings.ApiKey}"
        };

        // 构造请求体
        var base64Str = Convert.ToBase64String(request.ImageData);
        const string ocrType = "TEXT_DETECTION";
        var content = new
        {
            requests = new[]
            {
                new
                {
                    features = new[] { new { type = ocrType } },
                    image = new { content = base64Str }
                }
            }
        };

        // 发起请求
        var resp = await Context.HttpService.PostAsync(uriBuilder.Uri.ToString(), content, null, cancellationToken);
        if (string.IsNullOrEmpty(resp))
            throw new Exception("请求结果为空");

        // 解析响应
        var parsedData = JsonNode.Parse(resp) ?? throw new Exception($"反序列化失败: {resp}");

        // 判断是否出错
        if (parsedData["error"] is JsonNode error)
            return new OcrResult().Fail(error["message"]?.ToString() ?? "unknown error");

        // 提取词级结果（跳过第 0 项整图聚合文本）
        var textAnnotations = parsedData["responses"]?[0]?["textAnnotations"] as JsonArray;
        var ocrResult = new OcrResult();
        if (textAnnotations is null || textAnnotations.Count == 0)
            return ocrResult;

        for (var i = 1; i < textAnnotations.Count; i++)
        {
            var annotation = textAnnotations[i];
            var ocrContent = new OcrContent
            {
                Text = annotation["description"]?.ToString() ?? string.Empty
            };
            foreach (var point in Converter(annotation["boundingPoly"]?["vertices"] as JsonArray))
            {
                // 仅位置不全为 0 时添加
                if (point.X != point.Y || point.X != 0)
                    ocrContent.BoxPoints.Add(new BoxPoint(point.X, point.Y));
            }
            ocrResult.OcrContents.Add(ocrContent);
        }

        return ocrResult;
    }

    /// <summary>
    ///     将 Google Vision 的 vertices 转换为 BoxPoint 列表。
    ///     vertices 顺序：[0]左上 [1]右上 [2]右下 [3]左下；为 null 时返回 4 个 (0,0)。
    /// </summary>
    private static List<BoxPoint> Converter(JsonArray? vertices)
    {
        if (vertices is null || vertices.Count == 0)
            return [new BoxPoint(0, 0), new BoxPoint(0, 0), new BoxPoint(0, 0), new BoxPoint(0, 0)];

        return
        [
            // left top
            new BoxPoint(vertices[0]?["x"]?.GetValue<float>() ?? 0, vertices[0]?["y"]?.GetValue<float>() ?? 0),
            // right top
            new BoxPoint(vertices[1]?["x"]?.GetValue<float>() ?? 0, vertices[1]?["y"]?.GetValue<float>() ?? 0),
            // right bottom
            new BoxPoint(vertices[2]?["x"]?.GetValue<float>() ?? 0, vertices[2]?["y"]?.GetValue<float>() ?? 0),
            // left bottom
            new BoxPoint(vertices[3]?["x"]?.GetValue<float>() ?? 0, vertices[3]?["y"]?.GetValue<float>() ?? 0)
        ];
    }
}
```

> 说明：`BoxPoint` 构造函数签名为 `BoxPoint(float x, float y)`（见 `IOcrPlugin.cs:187`），Google Vision 的 `x`/`y` 为整数，用 `GetValue<float>()` 转换匹配。缺项用 `?? 0` 兜底。

- [ ] **Step 2: 提交**

```bash
git add src/Plugins/STranslate.Plugin.Ocr.Google/Main.cs
git commit -m "feat(ocr): implement GoogleOCR Main with Vision TEXT_DETECTION"
```

---

## Task 4: SettingsViewModel

**Files:**
- Create: `src/Plugins/STranslate.Plugin.Ocr.Google/ViewModel/SettingsViewModel.cs`

- [ ] **Step 1: 创建 SettingsViewModel.cs**

Create `src/Plugins/STranslate.Plugin.Ocr.Google/ViewModel/SettingsViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Ocr.Google.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext _context;
    private readonly Settings _settings;

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        _context = context;
        _settings = settings;

        Url = settings.Url;
        ApiKey = settings.ApiKey;

        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Url):
                _settings.Url = Url;
                break;
            case nameof(ApiKey):
                _settings.ApiKey = ApiKey;
                break;
            default:
                return;
        }
        _context.SaveSettingStorage<Settings>();
    }

    [ObservableProperty] public partial string Url { get; set; }
    [ObservableProperty] public partial string ApiKey { get; set; }

    public void Dispose() => PropertyChanged -= OnPropertyChanged;
}
```

- [ ] **Step 2: 提交**

```bash
git add src/Plugins/STranslate.Plugin.Ocr.Google/ViewModel/SettingsViewModel.cs
git commit -m "feat(ocr): add GoogleOCR SettingsViewModel"
```

---

## Task 5: 设置 UI（View）

**Files:**
- Create: `src/Plugins/STranslate.Plugin.Ocr.Google/View/SettingsView.xaml`
- Create: `src/Plugins/STranslate.Plugin.Ocr.Google/View/SettingsView.xaml.cs`

- [ ] **Step 1: 创建 SettingsView.xaml**

Create `src/Plugins/STranslate.Plugin.Ocr.Google/View/SettingsView.xaml`:

```xml
<UserControl
    x:Class="STranslate.Plugin.Ocr.Google.View.SettingsView"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:ikw="http://schemas.inkore.net/lib/ui/wpf"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:plugin="clr-namespace:STranslate.Plugin;assembly=STranslate.Plugin"
    xmlns:ui="http://schemas.inkore.net/lib/ui/wpf/modern"
    xmlns:vm="clr-namespace:STranslate.Plugin.Ocr.Google.ViewModel"
    d:DataContext="{d:DesignInstance Type=vm:SettingsViewModel}"
    d:DesignHeight="450"
    d:DesignWidth="800"
    mc:Ignorable="d">

    <ikw:SimpleStackPanel Spacing="12">
        <ui:SettingsCard Description="{DynamicResource STranslate_Plugin_Ocr_Google_Url_Description}" Header="{DynamicResource STranslate_Plugin_Ocr_Google_Url}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Link_24_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <TextBox MinWidth="300" Text="{Binding Url, UpdateSourceTrigger=PropertyChanged}" />
        </ui:SettingsCard>

        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Ocr_Google_ApiKey}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Key_24_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <PasswordBox
                MinWidth="300"
                plugin:PasswordBoxAssistant.Attach="True"
                plugin:PasswordBoxAssistant.Password="{Binding ApiKey}" />
        </ui:SettingsCard>

        <ui:SettingsCard Description="{DynamicResource STranslate_Plugin_Ocr_Google_Official_Description}" Header="{DynamicResource STranslate_Plugin_Ocr_Google_Official}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.WebAsset_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <ui:HyperlinkButton Content="https://cloud.google.com/vision" NavigateUri="https://cloud.google.com/vision" />
        </ui:SettingsCard>
    </ikw:SimpleStackPanel>
</UserControl>
```

- [ ] **Step 2: 创建 SettingsView.xaml.cs**

Create `src/Plugins/STranslate.Plugin.Ocr.Google/View/SettingsView.xaml.cs`:

```csharp
namespace STranslate.Plugin.Ocr.Google.View;

public partial class SettingsView
{
    public SettingsView() => InitializeComponent();
}
```

- [ ] **Step 3: 提交**

```bash
git add src/Plugins/STranslate.Plugin.Ocr.Google/View/SettingsView.xaml src/Plugins/STranslate.Plugin.Ocr.Google/View/SettingsView.xaml.cs
git commit -m "feat(ocr): add GoogleOCR settings UI"
```

---

## Task 6: 国际化资源（5 种语言）

**Files:**
- Create: `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/en.xaml`+`en.json`
- Create: `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/zh-cn.xaml`+`zh-cn.json`
- Create: `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/zh-tw.xaml`+`zh-tw.json`
- Create: `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/ja.xaml`+`ja.json`
- Create: `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/ko.xaml`+`ko.json`

资源键（xaml）：`STranslate_Plugin_Ocr_Google_Url`、`STranslate_Plugin_Ocr_Google_Url_Description`、`STranslate_Plugin_Ocr_Google_ApiKey`、`STranslate_Plugin_Ocr_Google_Official`、`STranslate_Plugin_Ocr_Google_Official_Description`。

- [ ] **Step 1: en.xaml + en.json**

Create `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/en.xaml`:

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:sys="clr-namespace:System;assembly=mscorlib">

    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Url">API Address</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Url_Description">Google Vision API address, default is the official address.</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_ApiKey">API Key</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Official">Official Website</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Official_Description">Click the link below to go to the official website for registration and use.</sys:String>

</ResourceDictionary>
```

Create `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/en.json`:

```json
{
  "Name": "Google OCR",
  "Description": "Google Vision OCR plugin for STranslate"
}
```

- [ ] **Step 2: zh-cn.xaml + zh-cn.json**

Create `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/zh-cn.xaml`:

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:sys="clr-namespace:System;assembly=mscorlib">

    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Url">API 地址</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Url_Description">谷歌 Vision API 地址，默认为官方地址。</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_ApiKey">API Key</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Official">官方网站</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Official_Description">点击下面连接跳转官方网站进行注册使用。</sys:String>

</ResourceDictionary>
```

Create `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/zh-cn.json`:

```json
{
  "Name": "谷歌 OCR",
  "Description": "适用于 STranslate 的谷歌 Vision OCR 插件"
}
```

- [ ] **Step 3: zh-tw.xaml + zh-tw.json**

Create `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/zh-tw.xaml`:

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:sys="clr-namespace:System;assembly=mscorlib">

    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Url">API 位址</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Url_Description">Google Vision API 位址，預設為官方位址。</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_ApiKey">API Key</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Official">官方網站</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Official_Description">點擊下面連結跳轉官方網站進行註冊使用。</sys:String>

</ResourceDictionary>
```

Create `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/zh-tw.json`:

```json
{
  "Name": "Google OCR",
  "Description": "適用於 STranslate 的 Google Vision OCR 插件"
}
```

- [ ] **Step 4: ja.xaml + ja.json**

Create `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/ja.xaml`:

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:sys="clr-namespace:System;assembly=mscorlib">

    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Url">API アドレス</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Url_Description">Google Vision API アドレス。デフォルトは公式アドレスです。</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_ApiKey">API Key</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Official">公式サイト</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Official_Description">下のリンクをクリックして公式サイトに移動し、登録してご利用ください。</sys:String>

</ResourceDictionary>
```

Create `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/ja.json`:

```json
{
  "Name": "Google OCR",
  "Description": "STranslate 向け Google Vision OCR プラグイン"
}
```

- [ ] **Step 5: ko.xaml + ko.json**

Create `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/ko.xaml`:

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:sys="clr-namespace:System;assembly=mscorlib">

    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Url">API 주소</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Url_Description">Google Vision API 주소입니다. 기본값은 공식 주소입니다.</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_ApiKey">API Key</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Official">공식 웹사이트</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Google_Official_Description">아래 링크를 클릭하여 공식 웹사이트로 이동하여 가입 후 사용하세요.</sys:String>

</ResourceDictionary>
```

Create `src/Plugins/STranslate.Plugin.Ocr.Google/Languages/ko.json`:

```json
{
  "Name": "Google OCR",
  "Description": "STranslate용 Google Vision OCR 플러그인"
}
```

- [ ] **Step 6: 提交**

```bash
git add src/Plugins/STranslate.Plugin.Ocr.Google/Languages/
git commit -m "feat(ocr): add GoogleOCR i18n resources (en/zh-cn/zh-tw/ja/ko)"
```

---

## Task 7: 维护 PrePluginIDs

**Files:**
- Modify: `src/STranslate/Core/Constant.cs:56-75`（`PrePluginIDs` 列表）

- [ ] **Step 1: 追加 GoogleOCR ID**

在 `PrePluginIDs` 列表 OCR 区段（`WeChatOCRBuiltIn` 那行之后）追加一行。Modify `src/STranslate/Core/Constant.cs`，将：

```csharp
        "3410e7de989340938301abd6fcf8cc4b", //WeChatOCRBuiltIn
        "86ec10628e754d41921d24387ec6e815", //Baidu
```

改为：

```csharp
        "3410e7de989340938301abd6fcf8cc4b", //WeChatOCRBuiltIn
        "2e83ee2f5dbf45249a3bd1457a326abf", //GoogleOCR
        "86ec10628e754d41921d24387ec6e815", //Baidu
```

- [ ] **Step 2: 提交**

```bash
git add src/STranslate/Core/Constant.cs
git commit -m "feat(ocr): register GoogleOCR in PrePluginIDs"
```

---

## Task 8: 编译验证

**Files:** 无（验证步骤）

- [ ] **Step 1: 编译整个解决方案**

Run:
```bash
dotnet build src/STranslate.slnx
```
Expected: BUILD SUCCEEDED，无错误。`STranslate.Plugin.Ocr.Google` 项目编译通过。

- [ ] **Step 2: 确认插件产物输出**

Run:
```bash
ls src/.artifacts/Debug/Plugins/STranslate.Plugin.Ocr.Google/
```
Expected: 包含 `STranslate.Plugin.Ocr.Google.dll`、`plugin.json`、`icon.png`、`Languages/` 目录。

- [ ] **Step 3: 如有编译错误则修复**

常见问题排查：
- XAML 命名空间/类名不匹配 → 检查 `x:Class` 与 namespace 一致
- `BoxPoint` 构造参数类型 → 确认用 `float`（`GetValue<float>()`）
- `IOcrPlugin`/`IPluginContext` 未找到 → 确认 csproj 的 `ProjectReference` 正确

修复后重新执行 Step 1，直至编译通过。

- [ ] **Step 4: 最终提交（如有修复）**

```bash
git add -A
git commit -m "fix(ocr): resolve GoogleOCR build issues" 2>/dev/null || echo "no fixes needed"
```

---

## 完成标准

- [ ] `dotnet build src/STranslate.slnx` 成功
- [ ] 插件产物输出到 `src/.artifacts/Debug/Plugins/STranslate.Plugin.Ocr.Google/`（含 dll/json/png/Languages）
- [ ] `plugin.json` 的 PluginID 与 `Constant.cs` 的 `PrePluginIDs` 条目一致（`2e83ee2f5dbf45249a3bd1457a326abf`）
- [ ] `src/STranslate.slnx` 已登记新项目
- [ ] 所有改动已提交
