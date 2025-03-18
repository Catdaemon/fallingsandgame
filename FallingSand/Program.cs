using System.Runtime;

GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

using var game = new FallingSand.Game1();
game.Run();
