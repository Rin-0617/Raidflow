using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;

namespace HelloWorldPlugin
{
    public class HelloWorldPlugin : IDalamudPlugin
    {
        private readonly IDalamudPluginInterface pluginInterface;
        private readonly WindowSystem windowSystem;
        private readonly HelloWorldWindow helloWorldWindow;

        public string Name => "HelloWorldPlugin";
        public string Author => "Your Name";
        public string Version => "1.0.0";

        public HelloWorldPlugin(IDalamudPluginInterface pluginInterface, IChatGui chatGui)
        {
            this.pluginInterface = pluginInterface;
            windowSystem = new WindowSystem("HelloWorldPlugin");

            helloWorldWindow = new HelloWorldWindow("Hello World", chatGui);
            windowSystem.AddWindow(helloWorldWindow);

            pluginInterface.UiBuilder.Draw += DrawUI;
        }

        public void Dispose()
        {
            windowSystem.RemoveAllWindows();
            pluginInterface.UiBuilder.Draw -= DrawUI;
        }

        private void DrawUI()
        {
            windowSystem.Draw();
        }
    }

    public class HelloWorldWindow : Window
    {
        private readonly IChatGui chatGui;

        public HelloWorldWindow(string name, IChatGui chatGui) : base(name)
        {
            this.chatGui = chatGui;
            IsOpen = true;
        }

        public override void Draw()
        {
            if (ImGui.Button("Hello World"))
            {
                chatGui.Print("Hello World!");
            }
        }
    }
}
