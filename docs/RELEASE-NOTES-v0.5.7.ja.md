# Eizo v0.5.7

## Stage 4：クロスプロバイダー識別バインド

- media-identity-bindings.json に EizoMedia と Bangumi/TMDB ID の対応を永続化します。
- 自動解決成功後の ExternalIds と Primary Provider を保存し、次回スキャンで再利用します。
- Manual Binding により Provider Subject ID を固定でき、手動指定は自動結果より優先されます。
- 手動ロックされた Provider ID は自動更新で上書きしません。
- MetadataService は保存済み Subject ID を使って正確な Subject/Episode を取得できます。
- RoutingReason に ManualIdentityBinding / PersistedIdentityBinding を記録します。
- 診断レポートに Binding 情報を追加します。

これにより、作品同定はスキャンごとの一時的な推測ではなく永続的なライブラリ状態になります。
