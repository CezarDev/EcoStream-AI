import type { TelemetryPoint } from "./types";

// Placeholder until the API exposes GET /api/telemetry (Phase 4).
const base = new Date("2026-11-01T00:00:00Z").getTime();

export const mockSeries: TelemetryPoint[] = Array.from({ length: 24 }, (_, h) => ({
  stationCode: "A801",
  latitude: -30.05,
  longitude: -51.17,
  measuredAt: new Date(base + h * 3_600_000).toISOString(),
  rainfallMmPerHour: Math.round((h > 12 && h < 18 ? 40 + h * 2 : 3 + (h % 5)) * 10) / 10,
  windGustKmh: 20 + (h % 7) * 6,
  pressureHpa: 1012 - (h > 12 ? h - 12 : 0) * 3,
}));
