# Markdown 渲染与纯文本复制

## 模块职责
- 在翻译结果输出区域支持 Markdown 格式渲染（标题、粗体、斜体、列表等）。
- 提供纯文本复制选项，移除 Markdown 语法符号保留列表结构。
- 修复 Markdown 内容滚轮滚动问题。
- 设置持久化到服务配置，支持每个服务独立设置。

## 关键入口
- `STranslate.Plugin/Service.cs`
  - `TranslationOptions.MarkdownRender` 属性：控制是否启用 Markdown 渲染。
  - `TranslationOptions.CopyAsPlainText` 属性：控制复制时是否转换为纯文本。
- `STranslate/Controls/OutputControl.xaml`
  - 使用 `mw:MarkdownViewer` (Markdig.Wpf) 渲染 Markdown。
  - 通过 `DataTrigger` 实现 Markdown/纯文本模式互斥切换。
- `STranslate/Controls/OutputControl.cs`
  - `EnableMouseWheelScroll` 附加属性：修复 Markdown 滚轮滚动问题。
- `STranslate/ViewModels/MainWindowViewModel.cs`
  - `Copy(object? param)` 方法：根据 `CopyAsPlainText` 设置决定复制内容。
  - `ConvertMarkdownToPlainText(string markdown)` 方法：使用 Markdig 解析器转换为纯文本。
- `STranslate/Services/BaseService.cs`
  - `MarkdownRender` 和 `CopyAsPlainText` 配置的持久化逻辑。

## 核心流程
### 从入口到结果：Markdown 渲染
1. 用户在服务设置中启用 `MarkdownRender`。
2. 翻译完成后，结果文本存入 `Plugin.TransResult.Text`。
3. `OutputControl.xaml` 中 `mw:MarkdownViewer` 绑定 `Markdown="{Binding Plugin.TransResult.Text}"` 自动渲染。
4. 通过 `DataTrigger` 控制 `mw:MarkdownViewer` 与 `TextBox` 互斥显示。

### 从入口到结果：纯文本复制
1. 用户点击复制按钮，触发 `CopyCommand`。
2. `MainWindowViewModel.Copy(object? param)` 接收 `Service` 对象。
3. 获取翻译结果文本：`text = service.Plugin.TransResult.Text`。
4. 若 `service.Options.CopyAsPlainText == true`，调用 `ConvertMarkdownToPlainText(text)` 转换为纯文本。
5. 使用 `ClipboardHelper.SetText(text)` 复制到剪贴板。

### 从入口到结果：设置持久化
1. 用户修改 `MarkdownRender` 或 `CopyAsPlainText` 设置。
2. `BaseService.UpdateOptions()` 方法捕获属性变更。
3. 将变更写入 `ServiceSettings` 并保存到 JSON 文件。
4. 重启后通过 `ServiceManager` 恢复设置。

### 从入口到结果：滚轮滚动修复
1. `OutputControl.xaml` 中 Markdown 区域设置 `OutputControl.EnableMouseWheelScroll="True"`。
2. `OutputControl.cs` 中 `OnPreviewMouseWheel` 拦截滚轮事件。
3. 手动将事件转发到父级 `ScrollViewer`：`PART_ContentScrollViewer.ScrollToVerticalOffset(...)`。
4. 设置 `e.Handled = true` 阻止事件继续传播。

## 关键数据结构/配置
- `TranslationOptions`
  - `MarkdownRender`：bool，默认 `false`，是否启用 Markdown 渲染。
  - `CopyAsPlainText`：bool，默认 `true`，是否复制纯文本。
- `OutputControl`
  - `CopyText`：string，复制按钮绑定的文本。
  - `CopyAsPlainText`：bool，是否启用纯文本复制模式。
  - `EnableMouseWheelScroll`：attached bool，启用滚轮转发。

## 关键文件
- `STranslate.Plugin/Service.cs`
- `STranslate/Controls/OutputControl.cs`
- `STranslate/Controls/OutputControl.xaml`
- `STranslate/Controls/ServicePanel.xaml`
- `STranslate/Services/BaseService.cs`
- `STranslate/Core/ServiceManager.cs`
- `STranslate/ViewModels/MainWindowViewModel.cs`
- `STranslate/Resources/CustomStyles.xaml`

## 常见改动任务
- 新增 Markdown 语法支持：
  1. 确认 Markdig.Wpf 是否原生支持（如不支持需扩展）。
  2. 在 `CustomStyles.xaml` 中调整样式。
- 修改纯文本转换逻辑：
  1. 修改 `ConvertMarkdownToPlainText` 方法。
  2. 考虑保留列表结构（数字列表 `1.`、无序列表 `•`）。
- 调整 Markdown/纯文本切换逻辑：
  1. 修改 `OutputControl.xaml` 中的 `DataTrigger`。
  2. 确保两种模式互斥且默认行为正确。
- 修改滚轮滚动行为：
  1. 调整 `OutputControl.cs` 中的 `OnPreviewMouseWheel` 实现。
  2. 评估 `e.Handled = true` 是否影响其他控件。

## 兼容性说明
- 旧版本服务配置不包含 `MarkdownRender` 和 `CopyAsPlainText` 时，默认值分别为 `false` 和 `true`。
- Markdown 渲染关闭时显示原始文本（包括 Markdown 符号）。
- CopyAsPlainText 关闭时复制原始文本不做任何修改。

## 依赖项
- `Markdig` (0.37.0)：Markdown 解析库。
- `Markdig.Wpf` (0.5.0.1)：WPF Markdown 渲染控件。