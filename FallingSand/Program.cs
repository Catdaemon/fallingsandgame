using System;
using System.Runtime;

GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

Environment.SetEnvironmentVariable("DYLD_PRINT_LIBRARIES", "1");

using var game = new FallingSand.Game1();
game.Run();
