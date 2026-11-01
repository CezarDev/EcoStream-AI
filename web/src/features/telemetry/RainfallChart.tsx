import { Bar, BarChart, CartesianGrid, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { TelemetryPoint } from "../../lib/types";

export function RainfallChart({ data }: { data: TelemetryPoint[] }) {
  const rows = data.map((d) => ({ hour: new Date(d.measuredAt).getUTCHours() + "h", rain: d.rainfallMmPerHour }));
  return (
    <ResponsiveContainer width="100%" height="100%">
      <BarChart data={rows} margin={{ top: 8, right: 8, bottom: 0, left: -16 }}>
        <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#334155" />
        <XAxis dataKey="hour" tick={{ fill: "#94a3b8", fontSize: 12 }} />
        <YAxis unit=" mm" tick={{ fill: "#94a3b8", fontSize: 12 }} />
        <Tooltip formatter={(v: number) => [`${v} mm/h`, "Chuva"]} />
        <ReferenceLine y={50} stroke="#dc2626" strokeDasharray="4 4" label={{ value: "alerta", fill: "#dc2626", fontSize: 12 }} />
        <Bar dataKey="rain" fill="#0ea5e9" radius={[3, 3, 0, 0]} />
      </BarChart>
    </ResponsiveContainer>
  );
}
