export type Severity = 1 | 2 | 3 | 4 | 5;

export interface TelemetryPoint {
  stationCode: string;
  latitude: number;
  longitude: number;
  measuredAt: string;
  rainfallMmPerHour: number;
  windGustKmh: number;
  pressureHpa: number;
}
