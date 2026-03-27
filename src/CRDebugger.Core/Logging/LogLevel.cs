namespace CRDebugger.Core.Logging;

/// <summary>
/// CRDebugger が扱うログレベルの列挙型。
/// 数値が大きいほど深刻度が高い。
/// </summary>
public enum CRLogLevel
{
    /// <summary>デバッグ情報。開発時の詳細なトレース出力に使用する</summary>
    Debug = 0,
    /// <summary>通常の情報メッセージ。一般的な動作状況を記録する</summary>
    Info = 1,
    /// <summary>警告。問題は発生していないが注意が必要な状況を示す</summary>
    Warning = 2,
    /// <summary>エラー。処理が失敗または例外が発生した場合に使用する</summary>
    Error = 3
}
