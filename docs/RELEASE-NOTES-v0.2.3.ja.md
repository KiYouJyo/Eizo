日本語 | [简体中文](RELEASE-NOTES-v0.2.3.md) | [English](RELEASE-NOTES-v0.2.3.en.md)

# Eizo v0.2.3 — 中幅ライトテーマのナビゲーション背景修正

このリリースは、v0.2.2 でも再現していた「中幅・ライトテーマで NavigationView を展開してから閉じると CompactPane が黒くなる」問題を修正します。

原因は WinUI NavigationView の VisualState にありました。

- CompactOverlay を開くと `PaneOverlaying` に入ります。
- 閉じると `PaneNotOverlaying` に入り、WinUI が `RootSplitView.PaneBackground` を `NavigationViewExpandedPaneBackground` へ戻します。
- Eizo は以前、`ShellNavigation.Resources` で `NavigationViewDefaultPaneBackground` と `NavigationViewExpandedPaneBackground` を `StaticResource` で上書きしていたため、NavigationView 作成時のテーマの Brush が固定されていました。
- そのため後からライトテーマへ切り替えても、次に Pane を閉じた瞬間、WinUI の状態遷移が作成時のダーク Brush を再設定してしまいました。

v0.2.3 では：

- 2 つの NavigationView Pane 背景上書きを削除しました。
- CompactOverlay が閉じている間は Eizo から `PaneBackground` を書き換えません。
- 閉じた状態は WinUI 標準の透明背景へ戻し、ウィンドウの Mica をそのまま表示します。
- Eizo の独自 surface は Pane を開く時、テーマ変更時、ウィンドウのアクティブ状態変更時だけ同期します。
- CI に回帰チェックを追加し、静的 Pane Brush の再導入や PaneClosed での再描画を禁止します。

v0.2.2 で修正したアップデーター、プレイヤーのレスポンシブ配置、プロジェクトリンク、カテゴリのアイコンと標準 Flyout の修正も引き続き含まれます。

> v0.2.2 からアプリ内更新で v0.2.3 へ更新し、ライトテーマ・中幅ウィンドウで「展開 → 閉じる」を繰り返して確認してください。
