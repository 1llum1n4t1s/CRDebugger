using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SuperLightLogger;

namespace CRDebugger.Core.Tests;

/// <summary>ホストのロガー構成と、無効化されたファサードの出力境界を検証する。</summary>
[Collection(CRDebuggerFacadeCollection.Name)]
public sealed class SuperLightLoggerFacadeTests : IDisposable
{
    public SuperLightLoggerFacadeTests() => CRDebugger.Shutdown();

    public void Dispose()
    {
        CRDebugger.Shutdown();
        LogManager.Reset();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Disabled_GetLogger_DoesNotReachHostLogger(bool generic)
    {
        var hostLogger = new Mock<ILogger>();
        hostLogger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var factory = new Mock<ILoggerFactory>();
        factory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(hostLogger.Object);
        LogManager.Configure(factory.Object, ownsFactory: false);
        CRDebugger.Initialize(new CRDebuggerOptions { IsEnabled = false });

        var logger = GetLogger(generic);
        logger.Info("無効化されたデバッガーからのログ");

        Assert.DoesNotContain(hostLogger.Invocations, i => i.Method.Name == "Log");
        factory.Verify(f => f.CreateLogger(It.IsAny<string>()), Times.Never);
        // ホスト自身による直接出力は引き続き有効である。
        LogManager.GetLogger("Host").Info("ホストのログ");
        Assert.Single(hostLogger.Invocations, i => i.Method.Name == "Log");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Disabled_GetLogger_AllMembersIgnoreMessagesAndFormatting(bool generic)
    {
        LogManager.Configure(NullLoggerFactory.Instance, ownsFactory: false);
        CRDebugger.Initialize(new CRDebuggerOptions { IsEnabled = false });
        var logger = GetLogger(generic);

        foreach (var property in typeof(ILog).GetProperties())
            Assert.Equal(false, property.GetValue(logger));

        // ILog の全オーバーロードで、メッセージ変換や書式評価まで省略されることを確認する。
        foreach (var method in typeof(ILog).GetMethods().Where(m => !m.IsSpecialName))
        {
            var args = method.GetParameters().Select(p => p.ParameterType switch
            {
                var type when type == typeof(string) => (object)"{invalid format",
                var type when type == typeof(object[]) => new object[] { new ExplodingMessage() },
                var type when type == typeof(Exception) => new InvalidOperationException("test"),
                var type when type == typeof(IFormatProvider) => new ExplodingFormatProvider(),
                var type when type == typeof(object) => new ExplodingMessage(),
                _ => throw new InvalidOperationException($"未対応の ILog 引数: {p}")
            }).ToArray();
            Assert.Null(Record.Exception(() => method.Invoke(logger, args)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BeforeInitialize_GetLogger_PreservesHostLoggerAccess(bool generic)
    {
        var hostLogger = new Mock<ILogger>();
        hostLogger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var factory = new Mock<ILoggerFactory>();
        factory.Setup(f => f.CreateLogger(typeof(SuperLightLoggerFacadeTests).FullName!))
            .Returns(hostLogger.Object);
        LogManager.Configure(factory.Object, ownsFactory: false);

        GetLogger(generic).Info("初期化前のホストログ");

        Assert.False(CRDebugger.IsInitialized);
        Assert.Single(hostLogger.Invocations, i => i.Method.Name == "Log");
    }

    private static ILog GetLogger(bool generic) => generic
        ? CRDebugger.GetLogger<SuperLightLoggerFacadeTests>()
        : CRDebugger.GetLogger(typeof(SuperLightLoggerFacadeTests));

    private sealed class ExplodingMessage
    {
        public override string ToString() => throw new InvalidOperationException("評価してはいけないメッセージ");
    }

    private sealed class ExplodingFormatProvider : IFormatProvider
    {
        public object GetFormat(Type? formatType) => throw new InvalidOperationException("評価してはいけない書式");
    }
}
