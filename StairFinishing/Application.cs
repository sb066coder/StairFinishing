using Nice3point.Revit.Toolkit.External;
using StairFinishing.Commands;

namespace StairFinishing
{
    /// <summary>
    ///     Application entry point
    /// </summary>
    [UsedImplicitly]
    public class Application : ExternalApplication
    {
        public override void OnStartup()
        {
            CreateRibbon();
        }

        private void CreateRibbon()
        {
            var panel = Application.CreatePanel("Commands", "StairFinishing");

            panel.AddPushButton<ReceiveFinishingArea>("Execute")
                .SetImage("/StairFinishing;component/Resources/Icons/RibbonIcon16.png")
                .SetLargeImage("/StairFinishing;component/Resources/Icons/RibbonIcon32.png");
        }
    }
}