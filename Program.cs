namespace PaintBot
{
    using System.Threading;
    using System.Threading.Tasks;

    class Program
    {
        static async Task Main(string[] args)
        {
            var myBot = new MyPaintbot();
            await myBot.Run(CancellationToken.None);
        }
    }
}
