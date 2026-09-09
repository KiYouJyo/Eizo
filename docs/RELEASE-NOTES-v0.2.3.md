简体中文 | [日本語](RELEASE-NOTES-v0.2.3.ja.md) | [English](RELEASE-NOTES-v0.2.3.en.md)

# Eizo v0.2.3 中尺寸浅色导航收起背景修复

本版本专门修复 v0.2.2 中仍可复现的中尺寸浅色模式 NavigationView 展开后再收起时，左侧 CompactPane 变黑的问题。

根因来自 WinUI NavigationView 的视觉状态机：

- 中尺寸 CompactOverlay 打开时进入 `PaneOverlaying`。
- 关闭时进入 `PaneNotOverlaying`，WinUI 会主动把 `RootSplitView.PaneBackground` 写回 `NavigationViewExpandedPaneBackground`。
- Eizo 之前在 `ShellNavigation.Resources` 中用 `StaticResource` 覆盖了 `NavigationViewDefaultPaneBackground` 和 `NavigationViewExpandedPaneBackground`，导致该 Brush 在控件构造时冻结到当时的主题。
- 当应用之后切换到浅色主题，Eizo 虽然能临时把 Pane 刷成浅色，但下一次收起时 WinUI 状态机会把冻结的深色 Brush 再写回去，于是 CompactPane 变黑。

v0.2.3 的修复方式：

- 删除这两个本地 NavigationView Pane 背景资源覆盖，不再冻结构造时主题。
- 关闭 CompactOverlay 时不再由 Eizo 重写 `PaneBackground`，完全交回 WinUI 原生 `PaneNotOverlaying` 状态。
- 关闭状态恢复使用 WinUI 原生透明背景，由窗口 Mica 自然透出。
- Eizo 只在 Pane 打开、主题变化和窗口激活状态变化时同步自定义展开 Pane surface。
- 新增 CI 回归守卫，禁止重新引入两个静态 Pane 背景别名或 PaneClosed 重绘逻辑。

同时保留 v0.2.2 中已经完成的更新器、播放器响应式布局、项目与开源卡片、分类图标与原生二级菜单修复。

> 推荐从 v0.2.2 使用应用内更新升级到 v0.2.3，然后在浅色模式、中尺寸窗口下反复执行“展开汉堡菜单 → 收起”进行验收。
