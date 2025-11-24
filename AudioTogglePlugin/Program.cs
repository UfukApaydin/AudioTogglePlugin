using BarRaider.SdTools;

namespace AudioTogglePlugin
{
    class Program
    {
        static void Main(string[] args)
        {
            // Connects to the Stream Deck software
            SDWrapper.Run(args);
        }
    }
}