# 设计：GoogleOCR 内置 OCR 插件

**日期**: 2026-06-26
**分支**: main
**状态**: 已批准，待实现

## 背景

STranslate 当前为 v2.0 插件化架构。1.0 分支中 `GoogleOCR.cs` 是内置 OCR 服务（继承 `OCRBase` 实现 `IOCR`，用 `Newtonsoft.Json` + `HttpUtil.PostAsync`，配置属性 `Url`/`AppKey`/`UseWordPosition` 直接挂在类上），但 v2.0 已重构为独立插件项目（实现 `IOcrPlugin` 接口，用 `System.Text.Json` + `IPluginContext.HttpService`，配置存到独立 `Settings` 类）。

需将 1.0 的 GoogleOCR 逻辑适配为 v2.0 内置 OCR 插件，调用 Google Cloud Vision API 的 `TEXT_DETECTION`，始终返回**词级文本 + 坐标框**（支持图片翻译）。

## 目标

- 新增内置 OCR 插件 `STranslate.Plugin.Ocr.Google`，纳入解决方案。
- 复用 1.0 的 Google Vision 调用与坐标解析逻辑，适配 v2.0 插件规范。
- 提供独立的 `Settings` 持久化与 WPF 设置 UI、国际化资源。

## 关键事实

1. **v2.0 OCR 插件接口**（`src/STranslate.Plugin/IOcrPlugin.cs`）：`IOcrPlugin : IPlugin`，需实现 `Init(IPluginContext)`、`Control GetSettingUI()`、`Dispose()`、`IEnumerable<LangEnum> SupportedLanguages`、`bool SupportBoxPoints() => false`（可 override）、`Task<OcrResult> RecognizeAsync(OcrRequest, CancellationToken)`。`OcrRequest` = `(byte[] ImageData, LangEnum Language, int PixelWidth, int PixelHeight)`；`OcrResult` 含 `OcrContents`（每项 `OcrContent{Text, BoxPoints}`）、`Regions`、`Fail(msg)` 等。
2. **内置插件规范**（参考 `STranslate.Plugin.Ocr.Baidu`）：扁平目录结构，`.csproj` 用 `ProjectReference` 引用 `STranslate.Plugin.csproj`，输出到 `.artifacts\Debug\Plugins\<PluginName>\`，含 `plugin.json`、`icon.png`、`Languages/*.{xaml,json}`、`Main.cs`、`Settings.cs`、`ViewModel/SettingsViewModel.cs`、`View/SettingsView.xaml(.cs)`，并在 `src/STranslate.slnx` 的 `/Plugins/` 文件夹登记。
3. **HttpService.PostAsync**（`IHttpService.cs:138`）：`Task<string> PostAsync(string url, object content, Options? options = null, CancellationToken cancellationToken = default)`，`content` 为对象时会自动序列化为 JSON（默认 `ContentType = "application/json"`）。
4. **Gemini OCR 插件**用 `System.Text.Json.Nodes.JsonNode` 按需取值（`Main.cs:215-219`），无需定义大量 DTO —— 本插件沿用此方式。
5. **plugin.json 格式**：`PluginID` 为 32 位无横线 GUID（如 Baidu `64f81251...`、Gemini `bd05beda...`），含 `Name`/`Description`/`Author`/`Version`/`Website`/`ExecuteFileName`/`IconPath`。
6. **Google Vision TEXT_DETECTION 响应**（来自 1.0 源码）：顶层 `error`（可选）；`responses[0].textAnnotations[]`，**第 0 项为整图聚合文本**，其后为词级；每项含 `description` 与 `boundingPoly.vertices[4]`（`{x,y}`，顺序：左上/右上/右下/左下）。`TEXT_DETECTION` 自动检测语言，不接收语言参数。

## 设计决策

- **位置**：内置插件（`src/Plugins/STranslate.Plugin.Ocr.Google/`），与 Baidu/OpenAI 一致。1.0 中 GoogleOCR 本就是内置服务。
- **始终词级**：不保留 1.0 的 `UseWordPosition` 开关，始终取 `textAnnotations[1..]` 作为词级结果（跳过第 0 项整图聚合）。
- **支持坐标框**：`SupportBoxPoints()` 返回 `true`，可设为图片翻译服务。
- **语言**：`SupportedLanguages` 返回 `Enum.GetValues<LangEnum>()`，`GetLanguage` 返回 `null`（Vision 自动检测，与 Gemini 插件一致）。不保留 1.0 的 `LangConverter` 映射表（其映射实际未传给 API）。
- **JSON 解析**：用 `JsonNode` 按需取值，不定义 1.0 中 `Root`/`Response`/`FullTextAnnotation`/`Page`/`Block`/`Paragraph`/`Word`/`Symbol` 等大量 DTO。
- **Settings 命名**：用 v2.0 风格 `Url`/`ApiKey`（与 Gemini/Baidu 一致），而非 1.0 的 `Url`/`AppKey`。
- **PluginID**：新生成 GUID `2e83ee2f5dbf45249a3bd1457a326abf`。
- **PrePluginIDs 维护**：作为内置插件，需在 `src/STranslate/Core/Constant.cs` 的 `PrePluginIDs` 列表追加该 ID（注释 `//GoogleOCR`），与 BaiduOCR/OpenAIOCR 等并列。
- **icon.png**：复制 Baidu 的 icon.png 占位，后续可替换。

## 目录结构

```
src/Plugins/STranslate.Plugin.Ocr.Google/
├── STranslate.Plugin.Ocr.Google.csproj
├── Main.cs
├── Settings.cs
├── plugin.json
├── icon.png
├── View/
│   ├── SettingsView.xaml
│   └── SettingsView.xaml.cs
├── ViewModel/
│   └── SettingsViewModel.cs
└── Languages/
    ├── en.xaml / en.json
    ├── zh-cn.xaml / zh-cn.json
    ├── zh-tw.xaml / zh-tw.json
    ├── ja.xaml / ja.json
    └── ko.xaml / ko.json
```

并在 `src/STranslate.slnx` 的 `/Plugins/` 文件夹 OCR 区段追加：
```xml
<Project Path="Plugins/STranslate.Plugin.Ocr.Google/STranslate.Plugin.Ocr.Google.csproj" />
```

## 组件设计

### Main.cs（`IOcrPlugin` 实现）

```csharp
namespace STranslate.Plugin.Ocr.Google;

public class Main : ObservableObject, IOcrPlugin
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public IEnumerable<LangEnum> SupportedLanguages => Enum.GetValues<LangEnum>();
    public bool SupportBoxPoints() => true;

    public void Init(IPluginContext context) { Context = context; Settings = context.LoadSettingStorage<Settings>(); }
    public Control GetSettingUI() { _viewModel ??= new(Context, Settings); _settingUi ??= new SettingsView { DataContext = _viewModel }; return _settingUi; }
    public void Dispose() => _viewModel?.Dispose();
    public string? GetLanguage(LangEnum langEnum) => null;

    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken)
    {
        // 1. 构造 URL: {Url}/v1/images:annotate?key={ApiKey}
        var uriBuilder = new UriBuilder(Settings.Url) { Path = "/v1/images:annotate", Query = $"key={Settings.ApiKey}" };

        // 2. 构造请求体
        var base64Str = Convert.ToBase64String(request.ImageData);
        var content = new { requests = new[] { new { features = new[] { new { type = "TEXT_DETECTION" } }, image = new { content = base64Str } } } };

        // 3. POST
        var resp = await Context.HttpService.PostAsync(uriBuilder.Uri.ToString(), content, null, cancellationToken);
        if (string.IsNullOrEmpty(resp)) throw new Exception("请求结果为空");

        // 4. 解析
        var parsed = JsonNode.Parse(resp) ?? throw new Exception($"反序列化失败: {resp}");
        if (parsed["error"] is JsonNode err) return new OcrResult().Fail(err["message"]?.ToString() ?? "unknown error");

        var textAnnotations = parsed["responses"]?[0]?["textAnnotations"] as JsonArray;
        var result = new OcrResult();
        if (textAnnotations is null || textAnnotations.Count == 0) return result;

        for (var i = 1; i < textAnnotations.Count; i++)  // 跳过第 0 项（整图聚合）
        {
            var ann = textAnnotations[i];
            var ocrContent = new OcrContent { Text = ann["description"]?.ToString() ?? "" };
            foreach (var pg in Converter(ann["boundingPoly"]?["vertices"] as JsonArray))
                if (pg.X != pg.Y || pg.X != 0)  // 全为 0 时跳过
                    ocrContent.BoxPoints.Add(new BoxPoint(pg.X, pg.Y));
            result.OcrContents.Add(ocrContent);
        }
        return result;
    }

    // vertices[0..3] -> 左上/右上/右下/左下，null 填 (0,0)*4
    private static List<BoxPoint> Converter(JsonArray? vertices) { ... }
}
```

> `Converter` 沿用 1.0 逻辑：`vertices` 为 null 时返回 4 个 `(0,0)`；否则按索引取 `[0]左上 [1]右上 [2]右下 [3]左下`，缺项视为 `(0,0)`。

### Settings.cs

```csharp
namespace STranslate.Plugin.Ocr.Google;
public class Settings
{
    public string Url { get; set; } = "https://vision.googleapis.com/";
    public string ApiKey { get; set; } = string.Empty;
}
```

### ViewModel/SettingsViewModel.cs

仿 Baidu：`ObservableObject, IDisposable`，持有 `Url`/`ApiKey` 属性，`PropertyChanged` 回写 `Settings` 并 `SaveSettingStorage<Settings>()`。

### View/SettingsView.xaml

三张 `ui:SettingsCard`：
1. API 地址（Url）—— `TextBox`
2. API Key（ApiKey）—— `PasswordBox` + `plugin:PasswordBoxAssistant`
3. 官网 —— `HyperlinkButton` → `https://cloud.google.com/vision`

### Languages（5 种语言）

资源键前缀 `STranslate_Plugin_Ocr_Google_`：`Url`、`Url_Description`、`ApiKey`、`Official`、`Official_Description`。`.json` 含 `Name`/`Description`。

### plugin.json

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

### .csproj

完全仿 Baidu 的 csproj：`TargetFramework=net10.0-windows`、`UseWPF=true`、`ProjectReference` 引用 `STranslate.Plugin.csproj`、输出路径 `.artifacts\Debug\Plugins\STranslate.Plugin.Ocr.Google\`、`Content` 包含 `Languages/*.*`、`icon.png`、`plugin.json`。

### Constant.cs 维护（`src/STranslate/Core/Constant.cs`）

作为内置插件，在 `PrePluginIDs` 列表 OCR 区段追加（与 BaiduOCR/OpenAIOCR/WeChatOCRBuiltIn 并列）：

```csharp
"2e83ee2f5dbf45249a3bd1457a326abf", //GoogleOCR
```

> 该 ID 必须与 `plugin.json` 的 `PluginID` 一致，否则内置插件无法被识别为预装插件。

## 错误处理

- 响应为空 → `throw new Exception("请求结果为空")`
- `JsonNode.Parse` 失败或返回 null → 抛出含原始响应的异常
- API 返回顶层 `error` → `OcrResult.Fail(message)`（不抛异常，保持 `IsSuccess=false`）
- `responses[0]` 或 `textAnnotations` 为 null/空 → 返回空 `OcrResult`（无识别内容）

## 不实现的内容（YAGNI）

- 不保留 `UseWordPosition` 开关（始终词级）
- 不保留 `LangConverter` 映射表（Vision 自动检测）
- 不定义 1.0 中一大套 DTO（用 `JsonNode` 按需取值）
- 不改动 `STranslate.Plugin` 框架、不改其他插件

## 验证

- `dotnet build src/STranslate.slnx` 编译通过
- 插件 DLL 输出到 `.artifacts\Debug\Plugins\STranslate.Plugin.Ocr.Google\`
- 设置 UI 可加载、Url/ApiKey 修改后持久化
- （可选，需 API Key）对测试图片调用返回词级文本 + 坐标框
