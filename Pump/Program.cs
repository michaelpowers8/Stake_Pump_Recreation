using System;
using Raylib_cs;
using System.Numerics;

public class BalloonGame
{
    // Game constants
    const int ScreenWidth = 800;
    const int ScreenHeight = 600;
    const float MaxBalloonSize = 300f;
    const float MinBalloonSize = 50f;
    const float PumpIncrement = 5f;
    const float DangerThreshold = 0.8f; // 80% of max size

    // Game state
    static float balloonSize = MinBalloonSize;
    static float tension = 0f;
    static float multiplier = 1.0f;
    static float balance = 1000f;
    static float currentBet = 10f;
    static bool balloonPopped = false;
    static bool gameRunning = true;
    static int pumps = 0;

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

    public static void Main()
    {
        // Initialize window
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "Balloon Pump Game");
        Raylib.SetTargetFPS(60);

        // Load textures and sounds
        Texture2D background = Raylib.LoadTexture("background.png"); // You'll need to provide this
        Sound pumpSound = Raylib.LoadSound("pump.wav");
        Sound popSound = Raylib.LoadSound("pop.wav");
        Sound cashSound = Raylib.LoadSound("cash.wav");

        // Game loop
        while (!Raylib.WindowShouldClose() && gameRunning)
        {
            // Update
            if (!balloonPopped)
            {
                HandleInput(pumpSound, popSound, cashSound);
                UpdateTension();
            }
            else if (Raylib.IsKeyPressed(KeyboardKey.Space))
            {
                ResetRound();
            }

            // Draw
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.White);

            // Draw background
            Raylib.DrawTexture(background, 0, 0, Color.White);

            // Draw UI elements
            DrawUI();

            if (!balloonPopped)
            {
                DrawBalloon();
                DrawTensionMeter();
            }
            else
            {
                DrawPoppedBalloon();
            }

            Raylib.EndDrawing();
        }

        // Cleanup
        Raylib.UnloadTexture(background);
        Raylib.UnloadSound(pumpSound);
        Raylib.UnloadSound(popSound);
        Raylib.UnloadSound(cashSound);
        Raylib.CloseWindow();
    }

    static void HandleInput(Sound pumpSound, Sound popSound, Sound cashSound)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.P))
        {
            // Pump the balloon
            balloonSize += PumpIncrement;
            tension = balloonSize / MaxBalloonSize;
            pumps++;

            // Change color every few pumps
            if (pumps % 3 == 0) currentColorIndex = (currentColorIndex + 1) % balloonColors.Length;

            // Update multiplier
            multiplier = 1.0f + (tension * 10f);

            Raylib.PlaySound(pumpSound);

            // Check if balloon pops
            if (tension >= 1.0f || (tension > DangerThreshold && new Random().NextDouble() < (tension - DangerThreshold)))
            {
                balloonPopped = true;
                balance -= currentBet;
                Raylib.PlaySound(popSound);
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.C))
        {
            // Cash out
            balance += currentBet * multiplier;
            Raylib.PlaySound(cashSound);
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

    static void UpdateTension()
    {
        // Random tension fluctuations to make it more exciting
        if (Raylib.GetRandomValue(0, 100) > 95)
        {
            tension += Raylib.GetRandomValue(-10, 10) / 1000f;
            tension = Math.Clamp(tension, 0f, 1f);
        }
    }

    static void DrawBalloon()
    {
        Color balloonColor = balloonColors[currentColorIndex];

        // Balloon wobble effect when tense
        float wobble = tension > 0.7f ? (float)Math.Sin(Raylib.GetTime() * 10) * (5 * tension) : 0;

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

        // Draw "POP!" text
        Raylib.DrawText("POP!", ScreenWidth / 2 - 50, ScreenHeight / 2 - 30, 60, Color.Red);
        Raylib.DrawText($"Lost ${currentBet}", ScreenWidth / 2 - 80, ScreenHeight / 2 + 40, 30, Color.Red);
        Raylib.DrawText("Press SPACE to continue", ScreenWidth / 2 - 150, ScreenHeight - 50, 20, Color.DarkGray);
    }

    static void DrawTensionMeter()
    {
        // Tension meter background
        Raylib.DrawRectangle(50, 50, 200, 30, Color.LightGray);

        // Tension level
        Color tensionColor = tension < 0.5f ? Color.Green :
                           tension < 0.8f ? Color.Yellow : Color.Red;

        Raylib.DrawRectangle(50, 50, (int)(200 * tension), 30, tensionColor);

        // Meter outline and label
        Raylib.DrawRectangleLines(50, 50, 200, 30, Color.Black);
        Raylib.DrawText("Tension Meter", 50, 20, 20, Color.Black);
    }

    static void DrawUI()
    {
        // Balance and bet info
        Raylib.DrawText($"Balance: ${balance:F2}", 20, ScreenHeight - 80, 20, Color.Black);
        Raylib.DrawText($"Current Bet: ${currentBet:F2}", 20, ScreenHeight - 50, 20, Color.Black);
        Raylib.DrawText($"Multiplier: {multiplier:F2}x", ScreenWidth - 200, ScreenHeight - 50, 20, Color.Black);
        Raylib.DrawText($"Pumps: {pumps}", ScreenWidth - 200, ScreenHeight - 80, 20, Color.Black);

        // Instructions
        if (!balloonPopped)
        {
            Raylib.DrawText("P: Pump  C: Cash Out", 20, ScreenHeight - 110, 20, Color.DarkGray);
            Raylib.DrawText("UP/DOWN: Change Bet", 20, ScreenHeight - 140, 20, Color.DarkGray);

            // Warning when tension is high
            if (tension > 0.7f)
            {
                float flash = (float)Math.Abs(Math.Sin(Raylib.GetTime() * 5));
                Raylib.DrawText("WARNING! High Tension!",
                    ScreenWidth / 2 - 150, 100, 30,
                    ColorFade(Color.Red, 0.5f + flash / 2));
            }
        }
    }

    static void ResetRound()
    {
        balloonSize = MinBalloonSize;
        tension = 0f;
        multiplier = 1.0f;
        balloonPopped = false;
        pumps = 0;
        currentColorIndex = (currentColorIndex + 1) % balloonColors.Length;
    }

    static Color ColorFade(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, (byte)(255 * alpha));
    }
}