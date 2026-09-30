using System.Collections;
using System.Reflection;
using OttoStash.Storing;

namespace OttoStash.Tests;

public class StorePauseTests
{
    private static int HandlerCount(ZNetView view)
    {
        FieldInfo table = typeof(ZNetView).GetField("m_functions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return ((IDictionary)table.GetValue(view)!).Count;
    }

    [Fact]
    public void The_game_refuses_a_second_handler_under_the_same_name()
    {
        ZNetView view = new();
        view.Register<bool>(StorePause.RpcName, (_, _) => { });

        Assert.Throws<ArgumentException>(() => view.Register<bool>(StorePause.RpcName, (_, _) => { }));
    }

    [Fact]
    public void Registering_the_pause_handler_twice_leaves_exactly_one()
    {
        ZNetView view = new();
        Container chest = new();

        StorePause.RegisterHandler(view, chest);
        StorePause.RegisterHandler(view, chest);

        Assert.Equal(1, HandlerCount(view));
    }

    [Fact]
    public void The_pause_handler_does_not_disturb_other_handlers()
    {
        ZNetView view = new();
        view.Register<long>("RPC_RequestOpen", (_, _) => { });

        StorePause.RegisterHandler(view, new Container());
        StorePause.RegisterHandler(view, new Container());

        Assert.Equal(2, HandlerCount(view));
    }
}
