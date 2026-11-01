import { StatCard } from "./components/StatCard";
import { RiskMap } from "./features/map/RiskMap";
import { RainfallChart } from "./features/telemetry/RainfallChart";
import { mockSeries } from "./lib/mockData";

export default function App() {
  const latest = mockSeries[mockSeries.length - 1];
  const peak = Math.max(...mockSeries.map((p) => p.rainfallMmPerHour));

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <header className="border-b border-slate-800 px-6 py-4">
        <h1 className="text-xl font-semibold">EcoStream AI</h1>
        <p className="text-sm text-slate-400">Monitoramento climático e resposta a emergências em tempo real</p>
      </header>

      <main className="grid gap-4 p-6 lg:grid-cols-3">
        <section className="grid grid-cols-2 gap-4 lg:col-span-3 lg:grid-cols-4">
          <StatCard label="Pico de chuva (24h)" value={`${peak} mm/h`} tone={peak >= 50 ? "danger" : "default"} />
          <StatCard label="Rajada atual" value={`${latest.windGustKmh} km/h`} />
          <StatCard label="Pressão atual" value={`${latest.pressureHpa} hPa`} />
          <StatCard label="Estações" value="1" />
        </section>

        <section className="h-[420px] lg:col-span-2">
          <RiskMap points={mockSeries.slice(-1)} />
        </section>

        <section className="h-[420px] rounded-xl border border-slate-800 bg-slate-900 p-4">
          <h2 className="mb-2 text-sm font-medium text-slate-300">Precipitação por hora</h2>
          <div className="h-[360px]">
            <RainfallChart data={mockSeries} />
          </div>
        </section>
      </main>
    </div>
  );
}
