namespace DiscordStatus.Utils
{
    public static class Util
    {
        public static void PrintLog(string message)
        {
            Console.WriteLine($"\u001b[34m[DiscordStatus] \u001b[32m{message}\u001b[0m");
        }

        public static void PrintError(string message)
        {
            Console.WriteLine($"\u001b[34m[DiscordStatus:ERROR] \u001b[32m{message}\u001b[0m");
        }
    }
}
