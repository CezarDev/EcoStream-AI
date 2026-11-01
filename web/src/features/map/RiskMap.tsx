import { CircleMarker, MapContainer, Popup, TileLayer } from "react-leaflet";
import type { TelemetryPoint } from "../../lib/types";

const RAIN_ALERT_MM = 50;

export function RiskMap({ points }: { points: TelemetryPoint[] }) {
  return (
    <MapContainer center={[-30.05, -51.17]} zoom={10} className="h-full w-full rounded-xl">
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />
      {points.map((p) => {
        const severe = p.rainfallMmPerHour >= RAIN_ALERT_MM;
        return (
          <CircleMarker
            key={`${p.stationCode}-${p.measuredAt}`}
            center={[p.latitude, p.longitude]}
            radius={severe ? 14 : 8}
            pathOptions={{ color: severe ? "#dc2626" : "#0284c7", fillOpacity: 0.5 }}
          >
            <Popup>
              <strong>{p.stationCode}</strong>
              <br />
              {p.rainfallMmPerHour} mm/h · {p.windGustKmh} km/h · {p.pressureHpa} hPa
            </Popup>
          </CircleMarker>
        );
      })}
    </MapContainer>
  );
}
