using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Raylib_cs;
using System.Numerics;
using System.Runtime.InteropServices;

public class BalloonPumpGame
{
    // Game constants
    const int ScreenWidth = 1920;
    const int ScreenHeight = 1080;
    const float MaxBalloonSize = 500f;
    const float MinBalloonSize = 80f;
    const float SizePerPump = (MaxBalloonSize - MinBalloonSize) / 20f; // Assuming max possible pumps is 20
    const float MenuBalloonSize = 120f;
    const float MenuBalloonWobbleSpeed = 2f;
    const float MenuBalloonWobbleAmount = 10f;

    // Multiplier tiers
    static readonly float[] Multipliers = new float[]
    {
        1.0f, 1.23f, 1.55f, 1.98f, 2.56f, 3.36f, 4.48f, 6.08f, 8.41f, 11.92f,
        17.34f, 26.01f, 40.46f, 65.74f, 112.70f, 206.62f, 413.23f, 929.77f,
        2479.40f, 8677.90f, 52067.40f
    };

    // Game state
    static float balloonSize = MinBalloonSize;
    static float balance = 1000f;
    static float currentBet = 1.00f;
    static bool balloonPopped = false;
    static bool gameRunning = true;
    static bool roundActive = false;
    static bool hasPumped = false;
    static int currentPumps = 0;
    static int maxPumps = 0;
    static int currentMultiplierIndex = 0;
    static bool isCashingOut = false;
    static float cashOutAnimationProgress = 0f;
    static float menuBalloonWobbleTime = 0f;
    static float popAnimationTimer = 0f;
    static bool showingPopAnimation = false;
    static Vector2 balloonPosition = new Vector2(ScreenWidth / 2, ScreenHeight / 2 - MinBalloonSize);
    static Vector2 balloonVelocity = Vector2.Zero;

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

    // UI elements
    static Rectangle playButton = new Rectangle(ScreenWidth / 2 - 100, ScreenHeight / 2 + 150, 200, 60);
    static Rectangle pumpButton = new Rectangle(ScreenWidth / 2 - 100, ScreenHeight / 2 + 230, 200, 60);
    static Rectangle cashOutButton = new Rectangle(ScreenWidth / 2 - 100, ScreenHeight / 2 + 310, 200, 60);
    static Rectangle betUpButton = new Rectangle(ScreenWidth - 120, ScreenHeight - 80, 50, 50);
    static Rectangle betDownButton = new Rectangle(ScreenWidth - 180, ScreenHeight - 80, 50, 50);
    static Rectangle rotateSeedButton = new Rectangle(ScreenWidth - 300, 10, 200, 40);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_HIDE = 0;

    public static void Main()
    {
        // Hide console window
        var handle = GetConsoleWindow();
        ShowWindow(handle, SW_HIDE);

        // Initialize window
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "Balloon Pump Game");
        Raylib.SetWindowState(ConfigFlags.FullscreenMode);
        Raylib.SetTargetFPS(60);

        // Generate initial seeds only once at startup
        if (string.IsNullOrEmpty(serverSeed))
        {
            serverSeed = GenerateServerSeed();
            clientSeed = GenerateClientSeed();
        }

        // Game loop
        while (!Raylib.WindowShouldClose() && gameRunning)
        {
            // Update
            HandleInput();
            UpdateCashOutAnimation(); // Add this line to update animations
            UpdateMenuBalloon();
            UpdatePopAnimation();

            // Draw
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.White);

            // Draw seed info at top
            DrawSeedInfo();

            // Draw game elements
            if (roundActive || isCashingOut || showingPopAnimation) // Modified this condition
            {
                DrawBalloon();
                if (!isCashingOut && !showingPopAnimation) // Only show UI if not in cash out animation
                {
                    DrawUI();
                }
            }
            else
            {
                DrawMainMenu();
            }

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }

    static void StartNewRound()
    {
        nonce++;
        maxPumps = CalculateMaxPumps();

        balloonSize = MinBalloonSize;
        currentPumps = 0;
        currentMultiplierIndex = 0;
        balloonPopped = false;
        roundActive = true;
        hasPumped = false; // Reset the pumped flag

        // Deduct bet from balance
        balance -= currentBet;
    }

    static void RotateSeeds()
    {
        serverSeed = GenerateServerSeed();
        clientSeed = GenerateClientSeed();
        nonce = 0; // Reset nonce when rotating seeds
    }

    // Replace the PumpBalloon method with this:
    static void PumpBalloon()
    {
        currentPumps++;
        hasPumped = true;
        balloonSize = MinBalloonSize + (SizePerPump * currentPumps);
        balloonSize = Math.Min(balloonSize, MaxBalloonSize);

        if (!balloonPopped && currentMultiplierIndex < Multipliers.Length - 1)
        {
            currentMultiplierIndex++;
        }

        if (currentPumps % 3 == 0)
        {
            currentColorIndex = (currentColorIndex + 1) % balloonColors.Length;
        }

        if (currentPumps >= maxPumps)
        {
            balloonPopped = true;
            roundActive = false;
            showingPopAnimation = true;
            popAnimationTimer = 0f; // Start the timer
        }
    }

    static void UpdatePopAnimation()
    {
        if (showingPopAnimation)
        {
            popAnimationTimer += Raylib.GetFrameTime();

            // After 1.5 seconds, end the animation
            if (popAnimationTimer >= 1.5f)
            {
                showingPopAnimation = false;
                balloonPopped = false;
            }
        }
    }

    static void CashOut()
    {
        if (roundActive && !balloonPopped && hasPumped)
        {
            isCashingOut = true;
            cashOutAnimationProgress = 0f;
            balloonVelocity = new Vector2(0, -5f); // Initial upward velocity

            // Apply winnings immediately (before animation completes)
            balance += currentBet * Multipliers[currentMultiplierIndex];
            roundActive = false;
            balloonPopped = false;
        }
    }

    static void UpdateCashOutAnimation()
    {
        if (!isCashingOut) return;

        cashOutAnimationProgress += 0.01f;

        // Apply gravity and wind effects
        balloonVelocity.Y -= 0.2f; // Gravity pulling down slightly
        balloonVelocity.X += (float)Math.Sin(Raylib.GetTime() * 5) * 0.5f; // Wobble side to side

        // Update position
        balloonPosition += balloonVelocity;

        // Make balloon shrink as it flies away
        balloonSize = Math.Max(balloonSize * 0.98f, 10f);

        // End animation when balloon is off screen or too small
        if (balloonPosition.Y < -balloonSize || balloonSize < 10f || cashOutAnimationProgress >= 1f)
        {
            isCashingOut = false;
        }
    }

    static void HandleInput()
    {
        Vector2 mousePos = Raylib.GetMousePosition();

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (!roundActive && Raylib.CheckCollisionPointRec(mousePos, playButton))
            {
                StartNewRound();
            }
            else if (roundActive && Raylib.CheckCollisionPointRec(mousePos, pumpButton))
            {
                PumpBalloon();
            }
            else if (roundActive && Raylib.CheckCollisionPointRec(mousePos, cashOutButton))
            {
                CashOut();
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, betUpButton) && currentBet < 10f && currentBet + 0.5f <= balance)
            {
                currentBet = Math.Min(currentBet + 0.5f, 10f);
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, betDownButton) && currentBet > 1f)
            {
                currentBet = Math.Max(currentBet - 0.5f, 1f);
            }
            else if (!roundActive && Raylib.CheckCollisionPointRec(mousePos, rotateSeedButton))
            {
                RotateSeeds();
            }
        }
    }

    static void DrawMainMenu()
    {
        // Draw the menu balloon
        float wobbleOffset = (float)Math.Sin(menuBalloonWobbleTime) * MenuBalloonWobbleAmount;
        int balloonCenterX = ScreenWidth / 2 + (int)wobbleOffset;
        int balloonCenterY = ScreenHeight / 2 - 200;

        // Draw balloon
        Raylib.DrawCircle(
            balloonCenterX,
            balloonCenterY,
            MenuBalloonSize / 2,
            balloonColors[currentColorIndex]
        );

        // Balloon highlight
        Raylib.DrawCircle(
            balloonCenterX - (int)(MenuBalloonSize / 6),
            balloonCenterY - (int)(MenuBalloonSize / 4),
            MenuBalloonSize / 8,
            ColorFade(Color.White, 0.8f)
        );

        // Balloon string
        Vector2 stringTop = new Vector2(
            balloonCenterX,
            balloonCenterY + MenuBalloonSize / 2
        );
        Vector2 stringBottom = new Vector2(
            balloonCenterX,
            balloonCenterY + MenuBalloonSize / 2 + MenuBalloonSize * 0.8f
        );

        Raylib.DrawLineEx(
            stringTop,
            stringBottom,
            2f,
            Color.Black
        );

        // Draw play button
        Raylib.DrawRectangleRec(playButton, Color.Green);
        Raylib.DrawText("PLAY", (int)playButton.X + 60, (int)playButton.Y + 15, 30, Color.White);

        // Draw bet controls
        Raylib.DrawText($"Bet: ${currentBet:F2}", ScreenWidth / 2 - 50, ScreenHeight / 2 + 100, 30, Color.Black);

        Raylib.DrawRectangleRec(betUpButton, Color.LightGray);
        Raylib.DrawText("+", (int)betUpButton.X + 15, (int)betUpButton.Y + 10, 30, Color.Black);

        Raylib.DrawRectangleRec(betDownButton, Color.LightGray);
        Raylib.DrawText("-", (int)betDownButton.X + 15, (int)betDownButton.Y + 10, 30, Color.Black);

        if (balloonPopped)
        {
            Raylib.DrawText("Balloon Popped!", ScreenWidth / 2 - 100, ScreenHeight / 2 - 200, 40, Color.Red);
        }

        // Always show balance and bet in bottom left
        DrawPersistentUI();
    }

    static void UpdateMenuBalloon()
    {
        if (!roundActive && !isCashingOut)
        {
            menuBalloonWobbleTime += Raylib.GetFrameTime() * MenuBalloonWobbleSpeed;
        }
    }

    static void DrawUI()
    {
        // Draw pump button
        Raylib.DrawRectangleRec(pumpButton, Color.Blue);
        Raylib.DrawText("PUMP", (int)pumpButton.X + 60, (int)pumpButton.Y + 15, 30, Color.White);

        // Draw cash out button - change color if disabled
        Color cashOutColor = hasPumped ? Color.Gold : Color.Gray;
        Raylib.DrawRectangleRec(cashOutButton, cashOutColor);
        Raylib.DrawText("CASH OUT", (int)cashOutButton.X + 30, (int)cashOutButton.Y + 15, 30,
            hasPumped ? Color.Black : Color.DarkGray);

        // Draw current multiplier
        Raylib.DrawText($"Multiplier: {Multipliers[currentMultiplierIndex]:F2}x",
            ScreenWidth / 2 - 100, ScreenHeight / 2 + 100, 30, Color.Black);

        // Always show balance and bet in bottom left
        DrawPersistentUI();
    }

    static void DrawPersistentUI()
    {
        // Draw balance and bet info in bottom left
        Raylib.DrawText($"Balance: ${balance:F2}", 20, ScreenHeight - 90, 30, Color.Black);
        Raylib.DrawText($"Current Bet: ${currentBet:F2}", 20, ScreenHeight - 50, 30, Color.Black);
    }

    static void DrawSeedInfo()
    {
        // Draw full hashed server seed, client seed, and formatted nonce at top
        string hashedServerSeed = Sha256Encrypt(serverSeed);
        Raylib.DrawText($"Server Seed Hash: {hashedServerSeed}", 20, 10, 20, Color.Black);
        Raylib.DrawText($"Client Seed: {clientSeed}", 20, 40, 20, Color.Black);
        Raylib.DrawText($"Nonce: {FormatNonce(nonce)}", 20, 70, 20, Color.Black);

        // Draw rotate seed button
        Raylib.DrawRectangleRec(rotateSeedButton, Color.SkyBlue);
        Raylib.DrawText("Rotate Seed", (int)rotateSeedButton.X + 30, (int)rotateSeedButton.Y + 10, 20, Color.Black);
    }

    static void DrawBalloon()
    {
        if (showingPopAnimation)
        {
            DrawPoppedBalloon();
            return;
        }

        Color balloonColor = balloonColors[currentColorIndex];

        // Use animated position if cashing out, otherwise calculate normally
        int balloonCenterX, balloonCenterY;

        if (isCashingOut)
        {
            balloonCenterX = (int)balloonPosition.X;
            balloonCenterY = (int)balloonPosition.Y;
        }
        else
        {
            // Regular wobble when not cashing out
            float wobble = currentPumps > 0 ? (float)Math.Sin(Raylib.GetTime() * 10) * 3f : 0;
            balloonCenterX = ScreenWidth / 2 + (int)wobble;
            balloonCenterY = ScreenHeight / 2 - (int)(balloonSize);
            balloonPosition = new Vector2(balloonCenterX, balloonCenterY);
        }

        // Draw balloon
        Raylib.DrawCircle(
            balloonCenterX,
            balloonCenterY,
            balloonSize / 2,
            balloonColor
        );

        // Balloon highlight
        Raylib.DrawCircle(
            balloonCenterX - (int)(balloonSize / 6),
            balloonCenterY - (int)(balloonSize / 4),
            balloonSize / 8,
            ColorFade(Color.White, 0.8f)
        );

        // Balloon string - only draw if not cashing out
        if (!isCashingOut)
        {
            Vector2 stringTop = new Vector2(
                balloonCenterX,
                balloonCenterY + balloonSize / 2
            );
            Vector2 stringBottom = new Vector2(
                balloonCenterX,
                balloonCenterY + balloonSize / 2 + balloonSize * 0.8f
            );

            Raylib.DrawLineEx(
                stringTop,
                stringBottom,
                2f,
                Color.Black
            );
        }
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
    }

    // Cryptographic functions
    static string Sha256Encrypt(string inputString)
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

    static List<string> SeedsToHexadecimals(string serverSeed, string clientSeed, int nonce)
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

    static List<byte> HexadecimalToBytes(string hexadecimal)
    {
        return Enumerable.Range(0, hexadecimal.Length)
                         .Where(x => x % 2 == 0)
                         .Select(x => Convert.ToByte(hexadecimal.Substring(x, 2), 16))
                         .ToList();
    }

    static int BytesToNumber(List<byte> bytesList, int multiplier)
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

    static Color ColorFade(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, (byte)(255 * alpha));
    }
}