using System;

namespace BatBot.Server.Models.Graph
{
    public class Swap
    {
        public string Id { get; set; }

        public Pair Pair { get; set; }

        public Transaction Transaction { get; set; }

        public decimal Amount0Out { get; set; }

        public decimal Amount1Out { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
