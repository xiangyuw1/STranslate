## 更新

- 添加：图片翻译精简窗口模式，悬浮于截图区域上方，工具栏按可用空间智能定位（左右扩展、上下翻转、居中或叠加），适用于紧凑场景
- 添加：图片翻译布局模式选择器，可在标准窗口与精简窗口间切换
- 添加：图片翻译窗口语种设置独立，不再影响主界面语种配置
- 添加：图片翻译支持选中文本复制，优化文本选中与复制体验
- 添加：主界面头部快捷服务切换器，无需打开设置即可快速切换翻译服务
- 添加：主界面可选「关闭显示」按钮
- 添加：隐藏输入框时触发输入翻译会临时显示输入框并聚焦，不再破坏用户的隐藏输入框设置
- 添加：DeepL 插件支持自定义 API 端点
- 添加：iCIBA 词典内置插件新增英式发音
- 添加：金山翻译（iCIBA Translate）与腾讯翻译（Transmart）内置插件
- 优化：OCR 智能分段逻辑，改进段落与表格识别
- 优化：OCR 结构化区域能力，`OcrResult` 新增 `Regions`（区域 → 段落 → 文本行）结构及坐标框
- 优化：百度 OCR 适配分段结果，开启段落识别并输出结构化布局
- 优化：图片翻译表格分段与长文本覆盖布局
- 优化：图片翻译覆盖层跟随软件主题与图片颜色，提高覆盖层不透明度
- 优化：通知策略由 Toast 改为 MessageBox / Snackbar，本地化 OCR 服务通知，感谢 @sean908 #711
- 优化：欢迎向导 UI 与图标，移除 TTS 配置步骤，调整向导服务插件顺序并加入主题切换与动画
- 优化：输入框关闭时隐藏语种选择控件
- 优化：通用设置界面图标与分隔符处理
- 优化：Transmart 内置插件恢复为可用服务（实测电信线路DNS解析有问题）

## 插件开发

- `STranslate.Plugin` 更新至 `1.0.12`
- `IOcrPlugin` 新增 `SupportBoxPoints()`，声明是否返回图片像素坐标文本框（图片翻译所需，默认 `false`）
- `OcrRequest` 新增 `PixelWidth` / `PixelHeight` 参数
- `OcrResult` 新增 `Regions` 结构化区域（`OcrRegion` → `OcrParagraph` → `OcrContent`），`Text` 在无 `OcrContents` 时回退按区域/段落拼装并智能补空格
- 新增 `TextHelper.IsCjk(char)`

## 其他

- [插件市场](https://stranslate.zggsong.com/plugins.html)
- [使用说明](https://stranslate.zggsong.com/docs/)
- [集成调用](https://stranslate.zggsong.com/docs/invoke.html)
- [安装卸载](https://stranslate.zggsong.com/docs/(un)install.html)
- [FAQ](https://stranslate.zggsong.com/docs/faq.html)

**完整更新日志:** [v2.0.7...v2.0.8](https://github.com/STranslate/STranslate/compare/v2.0.7...v2.0.8)
