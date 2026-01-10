# RichTextItem 实现记录

## 概述

RichTextItem 是一个高性能的 Unity UGUI 富文本组件，支持文本、图标、表情动画和可点击链接。

### 核心特性
- 文本渲染（支持颜色标签）
- 图标嵌入 `[icon:xxx]`
- 表情动画 `[emoji_xxx]`（支持帧动画）
- 可点击链接 `[link:id|text|color|style]`
- 阴影和描边特效
- **异步分帧渲染**（UniTask 实现，适合长文本）
- **高性能解析器**（Span 优化，减少 GC）

---

## 文件结构

```
RichTextItem/
├── RichTextItem.cs      # 主组件，布局和渲染逻辑
├── RichTextData.cs      # 数据结构定义
├── RichTextParser.cs    # 文本解析器（Span 优化）
├── RichTextConfig.cs    # 配置和资源加载集成
└── README.md            # 本文档
```

---

## 核心类说明

### RichTextItem.cs
主组件类，负责：
- Inspector 配置（字体、颜色、间距、特效等）
- 文本解析和布局计算
- UI 元素对象池管理
- 表情动画播放
- 链接点击处理
- **异步分帧渲染**

### RichTextData.cs
数据结构定义：
- `RichTextAlignment` - 文本对齐方式
- `RichTextElementType` - 元素类型（Text/Icon/Emoji/Link）
- `RichTextIconAlignment` - 图标垂直对齐
- `RichTextLinkStyle` - 链接样式
- `LinkData` - 链接数据
- `RichTextParams` - 渲染参数
- `RichTextElement` - 解析后的文本元素（对象池）
- `RichTextLayoutElement` - 布局元素
- `RichTextRow` - 行布局数据
- `EmojiAnimationData` - 表情帧动画数据
- `EmojiAnimationInstance` - 运行时表情动画实例

### RichTextParser.cs
高性能文本解析器：
- 使用 `ReadOnlySpan<char>` 减少字符串分配
- 支持颜色标签嵌套
- 支持图标、表情、链接标签解析

### RichTextConfig.cs
配置和资源加载：
- 表情注册和查询
- 与项目资源系统集成（`SetSpriteExtensions`）

---

## 标签格式

### 颜色标签
```
<color=#FF0000>红色文本</color>
<color=red>红色文本</color>
```

### 图标标签
```
[icon:diamond]      -> 加载名为 "diamond" 的图片
[icon:coin_gold]    -> 加载名为 "coin_gold" 的图片
```

### 表情标签（需先注册）
```
[emoji_001]         -> 播放已注册的表情动画
```

### 链接标签
```
[link:1|点击这里]                      -> 基础链接
[link:2|点击这里|#00BFFF]              -> 带颜色
[link:3|点击这里|#00BFFF|underline]    -> 带下划线
```

---

## 异步分帧渲染

### 设计背景
长文本场景下，单帧创建大量 UI 元素会导致卡顿。分帧渲染将工作分散到多帧执行。

### 配置参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `m_enableAsyncRendering` | false | 是否启用自动分帧 |
| `m_elementsPerFrame` | 10 | 每帧处理的元素数量 |
| `m_asyncThreshold` | 20 | 超过此数量自动启用分帧 |

### 使用方式

#### 方式1：自动异步（SetText）
```csharp
// 勾选 Enable Async Rendering 后，元素数量超过阈值自动启用
richText.SetText(longContent);
```

#### 方式2：显式异步（SetTextAsync）
```csharp
// 使用 UniTask
await richText.SetTextAsync(content, forceAsync: true, cancellationToken);
```

### 进度回调
```csharp
richText.OnRenderProgress = progress => {
    Debug.Log($"渲染进度: {progress:P0}");
};

richText.OnRenderComplete = () => {
    Debug.Log("渲染完成");
};
```

### 取消渲染
```csharp
richText.CancelRendering();
// 或
richText.SetText(newContent); // 自动取消之前的渲染
```

---

## 问题记录与解决方案

### 问题1：内存对象已被释放 (CancellationTokenSource)

**错误信息：**
```
DGameException: 内存对象已被释放过
```

**原因：**
在 `SetText` 的 fire-and-forget 模式中，`CancellationTokenSource` 在 `ContinueWith` 执行前被 `Dispose`。

**解决方案：**
在调用异步方法前捕获 Token：
```csharp
// 捕获当前的 CancellationToken，防止后续被替换
var token = m_cts.Token;

BuildLayoutAsync(elements, token)
    .ContinueWith(() =>
    {
        // 使用捕获的 token 而不是 m_cts.Token
        if (token.IsCancellationRequested)
        {
            // 处理取消...
        }
    })
    .Forget();
```

---

### 问题2：异步渲染受时间缩放影响

**问题描述：**
当 `Time.timeScale = 0` 时，异步渲染暂停。

**解决方案：**
使用 `PlayerLoopTiming.PreLateUpdate` 替代 `Update`：
```csharp
await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, token);
```
同时，表情动画使用 `Time.unscaledDeltaTime`：
```csharp
m_emojiTimer += Time.unscaledDeltaTime;
```

---

### 问题3：LateUpdate 操作正在被回收的资源

**错误信息：**
```
DGameException: 内存对象已被释放过
```
堆栈指向 `LateUpdate` -> `UpdateEmojiFrames`

**原因：**
异步渲染过程中，`LateUpdate` 尝试更新正在被回收的表情资源。

**解决方案：**
添加渲染状态检查：
```csharp
private void LateUpdate()
{
    if (m_pendingEffects)
    {
        m_pendingEffects = false;
        ApplyAllTextEffects();
    }

    // 异步渲染中，跳过表情更新
    if (m_isRendering) return;

    if (!m_hasEmojis || m_emojiInstances.Count == 0) return;
    // ...
}
```

---

### 问题4：表情动画加载取消错误

**错误信息：**
```
Failed to load asset 'xxx': System.OperationCanceledException
```

**原因：**
表情动画每帧请求加载精灵，当新请求到来时，资源系统取消旧请求。

**解决方案：**

1. **添加加载状态跟踪**（RichTextData.cs）：
```csharp
internal class EmojiAnimationInstance
{
    private bool m_isLoading;
    private string m_lastSpriteName;

    public void NextFrame(Action<UIImage, string, Action> setSprite)
    {
        // 如果上一帧还在加载中，跳过本次更新
        if (m_isLoading) return;

        var spriteName = AnimationData.GetFrame(CurrentFrame);

        // 只在精灵名称变化时才加载
        if (spriteName != m_lastSpriteName)
        {
            m_lastSpriteName = spriteName;
            m_isLoading = true;
            setSprite?.Invoke(TargetImage, spriteName, () => m_isLoading = false);
        }

        CurrentFrame = (CurrentFrame + 1) % AnimationData.FrameCount;
    }
}
```

2. **使用加载完成回调**（RichTextItem.cs）：
```csharp
instance.NextFrame((image, spriteName, onComplete) =>
{
    RichTextConfig.SetSprite(image, spriteName, false,
        _ => onComplete?.Invoke(), default);
});
```

---

## m_isRendering 状态管理

| 场景 | 设置 true | 设置 false |
|------|-----------|------------|
| SetText (同步) | 不设置 | 不设置 |
| SetText (自动异步) | 调用 BuildLayoutAsync 前 | ContinueWith 中（取消或完成） |
| SetTextAsync | try 块前 | finally 块 |
| CancelRendering | - | 直接设置 |
| LateUpdate | - | 检查后跳过 |

---

## 性能优化

### 1. Span 优化（RichTextParser.cs）
- 使用 `ReadOnlySpan<char>` 替代 `Substring`
- 使用 `Span.StartsWith` 替代 `String.StartsWith`
- 减少解析过程中的字符串分配

### 2. 对象池
- `RichTextElement` 使用静态对象池
- `RichTextLayoutElement` 使用静态对象池
- `RichTextRow` 使用静态对象池
- UI 元素（UIText/UIImage）使用实例对象池

### 3. 精灵缓存
- `EmojiAnimationInstance` 缓存上一次的精灵名称
- 只在精灵实际变化时才调用加载

### 4. 分帧阈值建议
- **移动端**: `elementsPerFrame = 5~8`
- **PC端**: `elementsPerFrame = 15~20`

---

## 使用示例

### 基础使用
```csharp
public class Example : MonoBehaviour
{
    public RichTextItem richText;

    void Start()
    {
        richText.SetText("普通文本 <color=#FF0000>红色</color> [icon:diamond]x100");
    }
}
```

### 链接点击
```csharp
richText.OnLinkClicked = linkData =>
{
    Debug.Log($"点击链接: ID={linkData.LinkID}, Text={linkData.LinkText}");
};
richText.SetText("[link:1|点击这里|#00BFFF|underline]");
```

### 表情注册
```csharp
// 注册表情帧
RichTextConfig.RegisterEmoji("[emoji_001]", "emoji_001_1", 1);
RichTextConfig.RegisterEmoji("[emoji_001]", "emoji_001_2", 2);
RichTextConfig.RegisterEmoji("[emoji_001]", "emoji_001_3", 3);

// 使用表情
richText.SetText("你好 [emoji_001]");
```

### 异步渲染长文本
```csharp
using Cysharp.Threading.Tasks;

public class LongTextExample : MonoBehaviour
{
    public RichTextItem richText;
    public Slider progressSlider;

    private CancellationTokenSource m_cts;

    async void Start()
    {
        m_cts = new CancellationTokenSource();

        richText.OnRenderProgress = p => progressSlider.value = p;
        richText.OnRenderComplete = () => Debug.Log("完成!");

        try
        {
            await richText.SetTextAsync(GenerateLongText(), true, m_cts.Token);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("渲染被取消");
        }
    }

    void OnDestroy()
    {
        m_cts?.Cancel();
        m_cts?.Dispose();
    }
}
```

---

## Inspector 配置说明

### Text Settings
| 属性 | 说明 |
|------|------|
| Font | 使用的字体 |
| Font Size | 字体大小 |
| Font Color | 默认文本颜色 |
| Support Rich Text | 是否支持富文本标签 |

### Icon Settings
| 属性 | 说明 |
|------|------|
| Icon Size | 图标尺寸 |
| Icon Offset | 图标偏移 |
| Icon Alignment | 图标垂直对齐（Center/Bottom/Top） |

### Layout Settings
| 属性 | 说明 |
|------|------|
| Alignment | 文本对齐（Left/Center/Right） |
| Character Spacing | 字符间距 |
| Line Spacing | 行间距 |
| Horizontal Overflow | 水平溢出（Wrap/Overflow） |
| Vertical Overflow | 垂直溢出（Truncate/Overflow） |

### Shadow Settings
| 属性 | 说明 |
|------|------|
| Enable Shadow | 启用阴影 |
| Shadow Effect Distance | 阴影偏移 |
| Shadow Colors | 四角阴影颜色 |

### Outline Settings
| 属性 | 说明 |
|------|------|
| Enable Outline | 启用描边 |
| Outline Color | 描边颜色 |
| Outline Width | 描边宽度 (1-10) |

### Animation
| 属性 | 说明 |
|------|------|
| Emoji FPS | 表情动画帧率 |

### Link Settings
| 属性 | 说明 |
|------|------|
| Default Link Color | 默认链接颜色 |
| Underline Height | 下划线高度 |

### Async Rendering
| 属性 | 说明 |
|------|------|
| Enable Async Rendering | 启用自动分帧 |
| Elements Per Frame | 每帧处理元素数 |
| Async Threshold | 启用分帧的阈值 |

---

## 版本历史

### v1.0 - 初始版本
- 基础富文本渲染
- 图标和表情支持
- 链接点击

### v1.1 - Span 优化
- 使用 `ReadOnlySpan<char>` 优化解析器
- 减少 GC 分配

### v1.2 - 异步分帧渲染
- 添加 UniTask 异步渲染支持
- 添加进度回调和取消支持
- 修复多个 CancellationToken 相关问题
- 修复表情动画加载取消错误
- 添加 `m_isRendering` 状态保护

### v1.3 - 性能深度优化与内存泄漏修复

#### 对象池扩展

| 类型 | 文件 | 说明 |
|------|------|------|
| `LinkData` | RichTextData.cs | 静态对象池，`Create()` / `Dispose()` |
| `EmojiAnimationInstance` | RichTextData.cs | 静态对象池 |
| 下划线 `UIImage` | RichTextItem.cs | 实例级对象池，复用下划线元素 |

#### StringBuilder 缓存

**文件**: RichTextItem.cs

```csharp
[ThreadStatic] private static StringBuilder s_textBuilder;
[ThreadStatic] private static StringBuilder s_colorBuilder;
```

- 避免 `ProcessTextElement`、`CreateTextLabel`、`CreateLinkLabel` 中频繁分配

#### RichTextParams 缓存

```csharp
private RichTextParams m_cachedParams;
```

- `CreateParamsFromInspector()` 复用缓存对象

#### IsEmojiSpan 键缓存

**文件**: RichTextConfig.cs

```csharp
private static readonly List<string> s_emojiKeyCache = new List<string>(32);
private static bool s_keyCacheDirty = true;
```

- 避免每次调用迭代 `Dictionary.Keys`
- 使用 `Span<char>.SequenceEqual` 避免字符串分配

#### 其他优化

- **Tab 替换**: 先 `Contains('\t')` 检查再 `Replace`
- **字符串比较**: `OrdinalIgnoreCase` 替代 `ToLower()`

#### Lambda 闭包消除

| 原位置 | 优化方式 |
|--------|----------|
| `EmojiAnimationInstance.NextFrame` 委托回调 | 改用 `TryGetNextFrame()` + `MarkLoadComplete()` |
| `button.onClick.AddListener` 闭包 | 新增 `LinkClickHandler` 组件 |
| `ProcessIconElement` SetSprite 回调 | `m_pendingIconSizes` 字典 + `OnIconSpriteLoaded` |
| `UpdateEmojiFramesForAnimation` 委托 | `OnEmojiSpriteLoaded` 实例方法回调 |
| `SetText` ContinueWith Lambda | `RenderAsyncInternal` 私有异步方法 |

#### 内存泄漏修复

**1. LinkData 重复 Dispose**

问题：链接换行时同一个 LinkData 被多次添加到列表，导致多次 Dispose

修复：拆分为两个列表
```csharp
private readonly List<UIText> m_linkTexts;        // 链接文本
private readonly List<LinkData> m_linkDataList;   // 唯一的 LinkData
```

**2. OnDestroy Action 回调清理**

```csharp
private void OnDestroy()
{
    OnLinkClicked = null;
    OnRenderProgress = null;
    OnRenderComplete = null;
    // ...
}
```

**3. OnDestroy reparent 错误修复**

问题：`Cannot set the parent of the GameObject 'Underline' while its new parent is being destroyed`

修复：OnDestroy 中不调用 `Clear()`，直接清理列表

#### 资源管理确认

| 资源 | 管理方式 | 状态 |
|------|----------|------|
| `CancellationTokenSource` | `CancelPendingOperations()` Cancel + Dispose | ✅ |
| `LinkData` | `m_linkDataList` 统一管理，单次 Dispose | ✅ |
| `EmojiAnimationInstance` | `RecycleEmojiInstances()` Dispose 回池 | ✅ |
| `RichTextRow/LayoutElement` | `ClearRows()` 级联 Dispose | ✅ |
| 按钮事件监听器 | `RemoveAllListeners()` | ✅ |
| `LinkClickHandler` | `Clear()` 清理引用 | ✅ |
| 图片资源引用 | `image.sprite = null` | ✅ |
| `m_pendingIconSizes` | `RecycleAllElements()` Clear | ✅ |
| Action 回调 | `OnDestroy` 设为 null | ✅ |

#### 潜在优化点（低优先级）

以下问题影响较小，仅在极端场景需要优化：

1. **GetComponent 调用**: `CreateLinkLabel`/`ClearLinkElements` 中可缓存
2. **OnEmojiSpriteLoaded 线性搜索**: 大量表情时可用 Dictionary 优化为 O(1)
3. **ColorUtility.ToHtmlStringRGB**: 创建临时字符串，可手动写入 StringBuilder
4. **button.onClick.AddListener**: 每次添加监听器创建委托实例

---

## 调试技巧

1. **检查渲染状态**: `richText.IsRendering`
2. **手动取消**: `richText.CancelRendering()`
3. **清空内容**: `richText.Clear()`
4. **获取尺寸**: `richText.Size`, `richText.Width`, `richText.Height`
5. **获取行数**: `richText.RowCount`

---

## 依赖

- Unity UGUI
- UniTask (Cysharp.Threading.Tasks)
- DGame 资源加载模块 (SetSpriteExtensions)
