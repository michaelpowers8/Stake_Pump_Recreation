using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

public class BalloonPumpGame
{
    // Helper method to compute SHA256 hash
    public static string Sha256Encrypt(string inputString)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(inputString));
            StringBuilder builder = new StringBuilder();
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }

    // Generate HMAC-SHA256 hashes from seeds and nonce
    public static List<string> SeedsToHexadecimals(string serverSeed, string clientSeed, int nonce)
    {
        List<string> messages = new List<string>
        {
            $"{clientSeed}:{nonce}:0",
            $"{clientSeed}:{nonce}:1",
            $"{clientSeed}:{nonce}:2"
        };

        List<string> hexDigests = new List<string>();
        foreach (string message in messages)
        {
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(serverSeed)))
            {
                byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                hexDigests.Add(BitConverter.ToString(hashBytes).Replace("-", "").ToLower());
            }
        }
        return hexDigests;
    }

    // Convert hexadecimal string to bytes
    public static List<byte> HexadecimalToBytes(string hexadecimal)
    {
        return Enumerable.Range(0, hexadecimal.Length)
                         .Where(x => x % 2 == 0)
                         .Select(x => Convert.ToByte(hexadecimal.Substring(x, 2), 16))
                         .ToList();
    }

    // Convert bytes to a weighted number
    public static int BytesToNumber(List<byte> bytesList, int multiplier)
    {
        double number = (
            (bytesList[0] / Math.Pow(256, 1)) +
            (bytesList[1] / Math.Pow(256, 2)) +
            (bytesList[2] / Math.Pow(256, 3)) +
            (bytesList[3] / Math.Pow(256, 4))
        );
        return (int)Math.Floor(number * multiplier);
    }

    // Generate a random server seed (64 hex chars)
    public static string GenerateServerSeed()
    {
        Random random = new Random();
        const string chars = "0123456789abcdef";
        return new string(Enumerable.Repeat(chars, 64)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    // Generate a random client seed (20 hex chars)
    public static string GenerateClientSeed()
    {
        Random random = new Random();
        const string chars = "0123456789abcdef";
        return new string(Enumerable.Repeat(chars, 20)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    // Generate game results from seeds
    public static List<int> SeedsToResults(string serverSeed, string clientSeed, int nonce, string difficulty)
    {
        List<int> shuffle = Enumerable.Range(0, 25).ToList();
        List<string> hexs = SeedsToHexadecimals(serverSeed, clientSeed, nonce);
        List<List<byte>> bytesLists = hexs.Select(HexadecimalToBytes).ToList();
        List<int> row = new List<int>();
        int multiplier = 25;

        foreach (List<byte> bytesList in bytesLists)
        {
            for (int i = 0; i < bytesList.Count; i += 4)
            {
                if (i + 4 <= bytesList.Count)
                {
                    row.Add(BytesToNumber(bytesList.GetRange(i, 4), multiplier));
                    multiplier--;
                }
            }
        }

        List<int> finalShuffle = new List<int>();
        for (int i = 0; i < row.Count; i++)
        {
            int num = row[i] % shuffle.Count;
            finalShuffle.Add(shuffle[num] + 1);
            shuffle.RemoveAt(num);
        }
        finalShuffle.Add(shuffle[0] + 1);
        return finalShuffle.Take(difficulty == "hard" ? 5 : 10).OrderBy(x => x).ToList();
    }

    // Calculate winnings based on results
    public static double CalculateWinnings(double bet, List<int> results, string difficulty)
    {
        double[] multipliersHard = new double[]
        {
            1.0, 1.23, 1.55, 1.98, 2.56, 3.36, 4.48, 6.08, 8.41, 11.92, 17.34, 26.01,
            40.46, 65.74, 112.70, 206.62, 413.23, 929.77, 2479.40, 8677.90, 52067.40
        };

        double[] multipliersEasy = new double[]
        {
            1.0, 1.63, 2.80, 4.95, 9.08, 17.34, 34.68, 73.21, 164.72, 400.02, 1066.73, 3200.18,
            11200.65, 48536.13, 291216.80, 3203384.80
        };

        double[] multipliers = difficulty == "hard" ? multipliersHard : multipliersEasy;
        return bet * multipliers[results.Min() - 1];
    }

    // Game state
    private static double balance = 1000;
    private static double currentBet = 0;
    private static double currentMultiplier = 1.0;
    private static int currentPumps = 0;
    private static string serverSeed = "";
    private static string clientSeed = "";
    private static int nonce = 0;
    private static bool isAnimating = false;

    // Colors
    private static ConsoleColor[] balloonColors = new ConsoleColor[]
    {
        ConsoleColor.Red,
        ConsoleColor.Yellow,
        ConsoleColor.Green,
        ConsoleColor.Cyan,
        ConsoleColor.Magenta
    };

    // Multipliers
    private static double[] multipliers = new double[]
    {
        1.0, 1.23, 1.55, 1.98, 2.56, 3.36, 4.48, 6.08, 8.41, 11.92, 17.34, 26.01,
        40.46, 65.74, 112.70, 206.62, 413.23, 929.77, 2479.40, 8677.90, 52067.40
    };

    // Get current game state
    private static (double multiplier, bool popped) GetCurrentGameState(int pumps)
    {
        List<int> shuffle = Enumerable.Range(0, 25).ToList();
        List<string> hexs = SeedsToHexadecimals(serverSeed, clientSeed, nonce);
        List<List<byte>> bytesLists = hexs.Select(HexadecimalToBytes).ToList();
        List<int> row = new List<int>();
        int multiplier = 25;

        foreach (List<byte> bytesList in bytesLists)
        {
            for (int i = 0; i < bytesList.Count; i += 4)
            {
                if (i + 4 <= bytesList.Count)
                {
                    row.Add(BytesToNumber(bytesList.GetRange(i, 4), multiplier));
                    multiplier--;
                }
            }
        }

        List<int> finalShuffle = new List<int>();
        for (int i = 0; i < row.Count; i++)
        {
            int num = row[i] % shuffle.Count;
            finalShuffle.Add(shuffle[num] + 1);
            shuffle.RemoveAt(num);
        }
        finalShuffle.Add(shuffle[0] + 1);

        int dangerNumber = finalShuffle.Take(5).Min();
        bool popped = pumps >= dangerNumber;

        double currentMultiplier = pumps < multipliers.Length ? multipliers[pumps] : multipliers.Last();

        return (currentMultiplier, popped);
    }

    // Draw the balloon with animation
    private static void DrawBalloon(int size, bool popped = false)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" BALANCE: ${balance:N2}");
        Console.WriteLine($"    BET: ${currentBet:N2}");
        Console.WriteLine();

        if (popped)
        {
            // Popped balloon animation
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("      ╔════════════╗");
            Console.WriteLine("      ║   BOOOOM!   ║");
            Console.WriteLine("      ╚════════════╝");
            Console.WriteLine();
            Console.WriteLine("    *     *     *     *");
            Console.WriteLine("  *   *   *   *   *   *");
            Console.WriteLine("    *     *     *     *");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"\nThe balloon popped after {currentPumps} pumps!");
            Console.WriteLine($"You lost ${currentBet:N2}");
            Console.WriteLine("\nPress any key to continue...");
            return;
        }

        // Choose balloon color based on pump count
        Console.ForegroundColor = balloonColors[currentPumps % balloonColors.Length];

        // Animated growing balloon
        string balloonTop = "  " + new string('_', size + 2);
        string balloonMiddle = " (" + new string(' ', size) + ")";
        string balloonBottom = "  " + new string('-', size + 2);

        Console.WriteLine(balloonTop);
        Console.WriteLine(balloonMiddle);
        Console.WriteLine(balloonBottom);

        // String with tension indicators
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        string tensionString = new string('=', currentPumps * 2);
        Console.WriteLine($"  {tensionString}||{tensionString}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("     |  |");
        Console.WriteLine("     |  |");

        // Game info
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"\n MULTIPLIER: {currentMultiplier:N2}x");
        Console.WriteLine($"     PUMPS: {currentPumps}");

        // Tension meter
        Console.Write(" TENSION: [");
        Console.ForegroundColor = GetTensionColor(currentPumps);
        Console.Write(new string('█', Math.Min(currentPumps, 20)));
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(new string(' ', Math.Max(0, 20 - currentPumps)) + "]");

        Console.WriteLine("\n [P] Pump  [C] Cash Out  [Q] Quit");
    }

    private static ConsoleColor GetTensionColor(int pumps)
    {
        if (pumps < 5) return ConsoleColor.Green;
        if (pumps < 10) return ConsoleColor.Yellow;
        if (pumps < 15) return ConsoleColor.DarkYellow;
        return ConsoleColor.Red;
    }

    // Animate balloon growing
    private static void AnimatePump()
    {
        isAnimating = true;
        Console.CursorVisible = false;

        try
        {
            int startSize = 5 + (currentPumps - 1) * 2;
            int endSize = 5 + currentPumps * 2;

            // Initial positions of elements we'll update
            int balloonTop = 3; // Line where balloon starts
            int infoLine = balloonTop + 6; // Line below balloon

            // Save static parts that don't change
            Console.SetCursorPosition(0, 0);
            Console.Write($" BALANCE: ${balance:N2}");
            Console.SetCursorPosition(0, 1);
            Console.Write($"    BET: ${currentBet:N2}\n");

            // Animation loop
            for (int size = startSize; size <= endSize; size++)
            {
                // Draw balloon
                Console.SetCursorPosition(0, balloonTop);
                Console.WriteLine("  " + new string('_', size + 2));
                Console.WriteLine(" (" + new string(' ', size) + ")");
                Console.WriteLine("  " + new string('-', size + 2));

                // Draw string
                string tensionString = new string('=', currentPumps * 2);
                Console.WriteLine($"  {tensionString}||{tensionString}");
                Console.WriteLine("     |  |");
                Console.WriteLine("     |  |");

                // Update info without clearing
                Console.SetCursorPosition(0, infoLine);
                Console.WriteLine($"\n MULTIPLIER: {currentMultiplier:N2}x    ");
                Console.WriteLine($"     PUMPS: {currentPumps}          ");

                Thread.Sleep(50);
            }

            // Vibration effect for high pumps
            if (currentPumps > 10)
            {
                for (int i = 0; i < 3; i++)
                {
                    Console.SetCursorPosition(1, balloonTop); // Slight offset
                    Console.WriteLine(new string('_', endSize + 3));
                    Thread.Sleep(100);
                    Console.SetCursorPosition(0, balloonTop); // Back to normal
                    Console.WriteLine("  " + new string('_', endSize + 2));
                    Thread.Sleep(100);
                }
            }
        }
        finally
        {
            Console.CursorVisible = true;
            isAnimating = false;
        }
    }

    // Animate balloon popping
    private static void AnimatePop()
    {
        isAnimating = true;
        int size = 5 + currentPumps * 2;

        // Shake before popping
        for (int i = 0; i < 5; i++)
        {
            DrawBalloon(size + (i % 2));
            Thread.Sleep(100);
        }

        // Pop animation frames
        for (int frame = 0; frame < 3; frame++)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("   .  *   .  *");
            Console.WriteLine(" *   .  *   .");
            Console.WriteLine("   .  *   .  *");
            Console.WriteLine(" *   .  *   .");
            Thread.Sleep(150);

            Console.Clear();
            Console.WriteLine("   *  .  *  .");
            Console.WriteLine(" .  *  .  *");
            Console.WriteLine("   *  .  *  .");
            Console.WriteLine(" .  *  .  *");
            Thread.Sleep(150);
        }

        DrawBalloon(size, true);
        isAnimating = false;
    }

    // Main game loop
    public static void Main()
    {
        Console.Title = "Balloon Pump Game";
        Console.CursorVisible = false;

        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════╗");
            Console.WriteLine("║   BALLOON PUMP GAME!    ║");
            Console.WriteLine("╚══════════════════════════╝");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($" Your balance: ${balance:N2}");

            if (balance <= 0)
            {
                Console.WriteLine("\nYou're out of money! Game over.");
                break;
            }

            Console.Write("\nEnter your bet (or Q to quit): ");
            string input = Console.ReadLine();

            if (input.ToLower() == "q") break;

            if (!double.TryParse(input, out currentBet) || currentBet <= 0 || currentBet > balance)
            {
                Console.WriteLine("Invalid bet amount!");
                Thread.Sleep(1000);
                continue;
            }

            balance -= currentBet;
            currentPumps = 0;
            serverSeed = GenerateServerSeed();
            clientSeed = GenerateClientSeed();
            nonce++;

            bool gameRunning = true;

            while (gameRunning && !isAnimating)
            {
                var result = GetCurrentGameState(currentPumps);
                currentMultiplier = result.multiplier;

                DrawBalloon(5 + currentPumps * 2);

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;

                    switch (key)
                    {
                        case ConsoleKey.P:
                            currentPumps++;
                            AnimatePump();
                            if (result.popped)
                            {
                                AnimatePop();
                                Console.ReadKey(true);
                                gameRunning = false;
                            }
                            break;

                        case ConsoleKey.C:
                            double winnings = currentBet * currentMultiplier;
                            balance += winnings;
                            Console.Clear();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"╔══════════════════════════╗");
                            Console.WriteLine($"║   CASHE OUT: {currentMultiplier:N2}x!  ║");
                            Console.WriteLine($"║   YOU WON ${winnings:N2}   ║");
                            Console.WriteLine($"╚══════════════════════════╝");
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine($"\nNew balance: ${balance:N2}");
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadKey(true);
                            gameRunning = false;
                            break;

                        case ConsoleKey.Q:
                            gameRunning = false;
                            break;
                    }
                }

                Thread.Sleep(50);
            }
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nThanks for playing!");
        Console.ForegroundColor = ConsoleColor.White;
        Console.CursorVisible = true;
    }
}