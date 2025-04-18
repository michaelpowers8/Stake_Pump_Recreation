using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Raylib_cs;
using System.Numerics;

public class BalloonPumpGame
{
    // Game constants
    const int ScreenWidth = 800;
    const int ScreenHeight = 600;
    const float MaxBalloonSize = 300f;
    const float MinBalloonSize = 50f;
    const float PumpIncrement = 5f;

    // Game state
    static float balloonSize = MinBalloonSize;
    static float multiplier = 1.0f;
    static float balance = 1000f;
    static float currentBet = 10f;
    static bool balloonPopped = false;
    static bool gameRunning = true;
    static int currentPumps = 0;
    static int maxPumps = 0;

    // Cryptographic state
    static string serverSeed = "";
    static string clientSeed = "";
    static int nonce = 0;

    // Colors
    static Color[] balloonColors = new Color[]
    {
        Color.Red,
        Color.Green,
        Color.Blue,
        Color.Yellow,
        Color.Purple,
        Color.Orange
    };
    static int currentColorIndex = 0;

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

    public static List<byte> HexadecimalToBytes(string hexadecimal)
    {
        return Enumerable.Range(0, hexadecimal.Length)
                         .Where(x => x % 2 == 0)
                         .Select(x => Convert.ToByte(hexadecimal.Substring(x, 2), 16))
                         .ToList();
    }

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

    public static string GenerateServerSeed()
    {
        byte[] data = RandomNumberGenerator.GetBytes(32);
        return BitConverter.ToString(data).Replace("-", "").ToLower();
    }

    public static string GenerateClientSeed()
    {
        byte[] data = RandomNumberGenerator.GetBytes(10);
        return BitConverter.ToString(data).Replace("-", "").ToLower();
    }

    static int CalculateMaxPumps()
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

        return finalShuffle.Take(5).Min();
    }

    static string FormatNonce(int n)
    {
        return n.ToString("N0");
    }

    public static void Main()
    {
        // Initialize window
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "Provably Fair Balloon Pump");
        Raylib.SetTargetFPS(60);

        // Generate initial seeds
        serverSeed = GenerateServerSeed();
        clientSeed = GenerateClientSeed();
        maxPumps = CalculateMaxPumps();

        // Game loop
        while (!Raylib.WindowShouldClose() && gameRunning)
        {
            // Update
            if (!balloonPopped)
            {
                HandleInput();
            }
            else if (Raylib.IsKeyPressed(KeyboardKey.Space))
            {
                ResetRound();
            }

            // Draw
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.White);

            // Draw seed info at top
            DrawSeedInfo();

            // Draw game elements
            if (!balloonPopped)
            {
                DrawBalloon();
            }
            else
            {
                DrawPoppedBalloon();
            }

            DrawUI();
            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }

    static void DrawSeedInfo()
    {
        // Draw full hashed server seed, client seed, and formatted nonce at top
        string hashedServerSeed = Sha256Encrypt(serverSeed);
        Raylib.DrawText($"Server Seed Hash: {hashedServerSeed}", 20, 10, 20, Color.Black);
        Raylib.DrawText($"Client Seed: {clientSeed}", 20, 40, 20, Color.Black);
        Raylib.DrawText($"Nonce: {FormatNonce(nonce)}", 20, 70, 20, Color.Black);
    }

    static void HandleInput()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.P))
        {
            currentPumps++;
            balloonSize = MinBalloonSize + (MaxBalloonSize - MinBalloonSize) * (currentPumps / (float)maxPumps);

            // Update multiplier based on pumps
            multiplier = 1.0f + (10f * (currentPumps / (float)maxPumps));

            // Change color every few pumps
            if (currentPumps % 3 == 0) currentColorIndex = (currentColorIndex + 1) % balloonColors.Length;

            // Check if balloon should pop
            if (currentPumps >= maxPumps)
            {
                balloonPopped = true;
                balance -= currentBet;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.C))
        {
            // Cash out
            balance += currentBet * multiplier;
            ResetRound();
        }

        // Adjust bet
        if (Raylib.IsKeyPressed(KeyboardKey.Up) && currentBet < balance)
        {
            currentBet = Math.Min(currentBet + 10f, balance);
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Down) && currentBet > 10f)
        {
            currentBet = Math.Max(currentBet - 10f, 10f);
        }
    }

    static void DrawBalloon()
    {
        Color balloonColor = balloonColors[currentColorIndex];

        // Balloon wobble effect when getting close to max
        float progress = currentPumps / (float)maxPumps;
        float wobble = progress > 0.7f ? (float)Math.Sin(Raylib.GetTime() * 10) * (5 * progress) : 0;

        // Draw balloon
        Raylib.DrawCircle(
            (int)(ScreenWidth / 2 + wobble),
            (int)(ScreenHeight / 2 - balloonSize / 3),
            balloonSize / 2,
            balloonColor
        );

        // Balloon highlight
        Raylib.DrawCircle(
            (int)(ScreenWidth / 2 - balloonSize / 6 + wobble),
            (int)(ScreenHeight / 2 - balloonSize / 2.5f),
            balloonSize / 8,
            ColorFade(Color.White, 0.8f)
        );

        // Balloon string
        Raylib.DrawLineEx(
            new Vector2(ScreenWidth / 2 + wobble, ScreenHeight / 2 + balloonSize / 3),
            new Vector2(ScreenWidth / 2, ScreenHeight / 2 + balloonSize / 2),
            2f,
            Color.Black
        );
    }

    static void DrawPoppedBalloon()
    {
        // Draw explosion particles
        for (int i = 0; i < 30; i++)
        {
            float angle = Raylib.GetRandomValue(0, 360) * Raylib.DEG2RAD;
            float distance = Raylib.GetRandomValue(0, 100);
            Vector2 pos = new Vector2(
                ScreenWidth / 2 + (float)Math.Cos(angle) * distance,
                ScreenHeight / 2 + (float)Math.Sin(angle) * distance
            );

            Raylib.DrawCircleV(pos, Raylib.GetRandomValue(2, 8),
                balloonColors[Raylib.GetRandomValue(0, balloonColors.Length - 1)]);
        }

        // Draw result text
        Raylib.DrawText("POP!", ScreenWidth / 2 - 50, ScreenHeight / 2 - 30, 60, Color.Red);
        Raylib.DrawText($"Lost ${currentBet}", ScreenWidth / 2 - 80, ScreenHeight / 2 + 40, 30, Color.Red);
        Raylib.DrawText("Press SPACE to continue", ScreenWidth / 2 - 150, ScreenHeight - 50, 20, Color.DarkGray);
    }

    static void DrawUI()
    {
        // Balance and bet info at bottom
        Raylib.DrawText($"Balance: ${balance:F2}", 20, ScreenHeight - 80, 20, Color.Black);
        Raylib.DrawText($"Current Bet: ${currentBet:F2}", 20, ScreenHeight - 50, 20, Color.Black);
        Raylib.DrawText($"Multiplier: {multiplier:F2}x", ScreenWidth - 200, ScreenHeight - 50, 20, Color.Black);

        // Instructions
        if (!balloonPopped)
        {
            Raylib.DrawText("P: Pump  C: Cash Out", 20, ScreenHeight - 110, 20, Color.DarkGray);
            Raylib.DrawText("UP/DOWN: Change Bet", 20, ScreenHeight - 140, 20, Color.DarkGray);
        }
    }

    static void ResetRound()
    {
        // Generate new seeds and calculate new max pumps
        nonce++;
        serverSeed = GenerateServerSeed();
        clientSeed = GenerateClientSeed();
        maxPumps = CalculateMaxPumps();

        // Reset game state
        balloonSize = MinBalloonSize;
        currentPumps = 0;
        balloonPopped = false;
        currentColorIndex = (currentColorIndex + 1) % balloonColors.Length;
    }

    static Color ColorFade(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, (byte)(255 * alpha));
    }
}