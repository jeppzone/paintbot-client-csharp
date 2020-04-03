namespace PaintBot.Game.Result
{
    public class PlayerPoints
    {
        public string Name { get; set; }
        public string PlayerId { get; set; }
        public int Points { get; set; }

        public override string ToString()
        {
            return $"{Name} (${PlayerId}) - {Points} points";
        }
    }
}