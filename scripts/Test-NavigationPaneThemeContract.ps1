$ErrorActionPreference = 'Stop'

$mainWindow = Get-Content -LiteralPath 'src/Eizo.App/MainWindow.xaml' -Raw
$shellChrome = Get-Content -LiteralPath 'src/Eizo.App/MainWindow.ShellChrome.cs' -Raw

$forbiddenResourceKeys = @(
    'x:Key="NavigationViewDefaultPaneBackground"',
    'x:Key="NavigationViewExpandedPaneBackground"'
)

foreach ($key in $forbiddenResourceKeys) {
    if ($mainWindow.Contains($key)) {
        throw "Navigation pane theme contract violation: $key must not be locally overridden. NavigationView changes PaneBackground when CompactOverlay closes; a StaticResource alias can freeze the construction-time theme."
    }
}

if ($shellChrome -match 'ShellNavigation\.PaneClosed\s*\+=') {
    throw 'Navigation pane theme contract violation: PaneClosed must not repaint SplitView.PaneBackground. Closed CompactOverlay should return to WinUI native transparent pane background.'
}

if ($shellChrome -notmatch 'ShellNavigation\.PaneOpening\s*\+=' -or
    $shellChrome -notmatch 'ShellNavigation\.ActualThemeChanged\s*\+=') {
    throw 'Navigation pane theme contract violation: PaneOpening and ActualThemeChanged refresh hooks are required.'
}

if ($shellChrome -notmatch 'if \(!ShellNavigation\.IsPaneOpen\) return;') {
    throw 'Navigation pane theme contract violation: closed CompactOverlay must remain owned by WinUI and must not be repainted by Eizo.'
}

Write-Host 'Navigation pane theme contract PASS.'
