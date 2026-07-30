'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import {
  BusinessOverview,
  businessApi,
} from '@/lib/api';
import { isAdminAuthenticated, loginAdmin } from '@/lib/auth';

type PeriodDays = 7 | 30 | 90 | 365;

function formatEur(value: number): string {
  return new Intl.NumberFormat('pt-PT', {
    style: 'currency',
    currency: 'EUR',
    maximumFractionDigits: 2,
  }).format(value);
}

function formatPct(value: number): string {
  const sign = value > 0 ? '+' : '';
  return `${sign}${value.toFixed(1)}%`;
}

function formatShortDate(date: string): string {
  const d = new Date(`${date}T00:00:00Z`);
  return d.toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit' });
}

function simulate(params: {
  price: number;
  sourceCost: number;
  fillsPerSource: number;
  monthlyFills: number;
}) {
  const costPerFill = params.fillsPerSource > 0 ? params.sourceCost / params.fillsPerSource : 0;
  const profitPerFill = params.price - costPerFill;
  const margin = params.price > 0 ? (profitPerFill / params.price) * 100 : 0;
  const monthlyRevenue = params.monthlyFills * params.price;
  const monthlyProfit = params.monthlyFills * profitPerFill;
  return {
    costPerFill,
    profitPerFill,
    margin,
    monthlyRevenue,
    monthlyProfit,
    yearlyProfit: monthlyProfit * 12,
  };
}

export default function BusinessPage() {
  const t = useTranslations();
  const [unlocked, setUnlocked] = useState(false);
  const [adminUser, setAdminUser] = useState('');
  const [adminPass, setAdminPass] = useState('');
  const [loginError, setLoginError] = useState(false);
  const [days, setDays] = useState<PeriodDays>(30);
  const [overview, setOverview] = useState<BusinessOverview | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [settingsSaved, setSettingsSaved] = useState(false);
  const [showSettings, setShowSettings] = useState(false);

  // Simulator state
  const [simPrice, setSimPrice] = useState(10);
  const [simVolume, setSimVolume] = useState(150);
  const [simSourceCost, setSimSourceCost] = useState(90);
  const [volumeTouched, setVolumeTouched] = useState(false);

  // Settings form
  const [formPrice, setFormPrice] = useState(10);
  const [formSourceCost, setFormSourceCost] = useState(90);
  const [formSourceKg, setFormSourceKg] = useState(17);
  const [formConsumerG, setFormConsumerG] = useState(425);

  useEffect(() => {
    setUnlocked(isAdminAuthenticated());
  }, []);

  useEffect(() => {
    if (!unlocked) return;

    let cancelled = false;
    setLoading(true);
    setError(null);

    businessApi
      .getOverview(days)
      .then((data) => {
        if (cancelled) return;
        setOverview(data);
        setFormPrice(data.settings.refillPriceEur);
        setFormSourceCost(data.settings.sourceCylinderCostEur);
        setFormSourceKg(data.settings.sourceCylinderGasKg);
        setFormConsumerG(data.settings.consumerCylinderGasG);
        setSimPrice(data.settings.refillPriceEur);
        setSimSourceCost(data.settings.sourceCylinderCostEur);
        if (!volumeTouched) {
          setSimVolume(Math.max(1, Math.round(data.averageDailyFills * 30)));
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : t('business.loadError'));
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [unlocked, days, t, volumeTouched]);

  const currentSim = useMemo(() => {
    if (!overview) return null;
    return simulate({
      price: overview.settings.refillPriceEur,
      sourceCost: overview.settings.sourceCylinderCostEur,
      fillsPerSource: overview.settings.fillsPerSourceCylinder,
      monthlyFills: simVolume,
    });
  }, [overview, simVolume]);

  const simulated = useMemo(() => {
    if (!overview) return null;
    return simulate({
      price: simPrice,
      sourceCost: simSourceCost,
      fillsPerSource: overview.settings.fillsPerSourceCylinder,
      monthlyFills: simVolume,
    });
  }, [overview, simPrice, simSourceCost, simVolume]);

  const insights = useMemo(() => {
    if (!overview || !currentSim || !simulated) return [];

    const items: Array<{ severity: 'info' | 'success' | 'warning'; text: string }> = [];

    if (overview.fillsChangePercent >= 15) {
      items.push({
        severity: 'success',
        text: t('business.insights.growth', { pct: formatPct(overview.fillsChangePercent) }),
      });
    } else if (overview.fillsChangePercent <= -15) {
      items.push({
        severity: 'warning',
        text: t('business.insights.decline', { pct: formatPct(overview.fillsChangePercent) }),
      });
    }

    if (overview.daysUntilNextSourceCylinder > 0 && overview.daysUntilNextSourceCylinder <= 7) {
      items.push({
        severity: 'warning',
        text: t('business.insights.sourceSoon', { days: overview.daysUntilNextSourceCylinder }),
      });
    }

    if (overview.pipelineReadyCount > 0) {
      items.push({
        severity: 'info',
        text: t('business.insights.pipeline', {
          count: overview.pipelineReadyCount,
          value: formatEur(overview.pipelineValue),
        }),
      });
    }

    if (overview.unpaidCompletedOrders > 0) {
      items.push({
        severity: 'warning',
        text: t('business.insights.unpaid', { count: overview.unpaidCompletedOrders }),
      });
    }

    if (overview.problemCylinders > 0) {
      items.push({
        severity: 'warning',
        text: t('business.insights.problems', { count: overview.problemCylinders }),
      });
    }

    const priceDelta = simPrice - overview.settings.refillPriceEur;
    if (Math.abs(priceDelta) >= 0.5) {
      const monthlyDelta = simulated.monthlyProfit - currentSim.monthlyProfit;
      items.push({
        severity: monthlyDelta >= 0 ? 'success' : 'warning',
        text: t('business.insights.priceSim', {
          price: formatEur(simPrice),
          delta: formatEur(monthlyDelta),
        }),
      });
    }

    if (overview.topCustomers[0]) {
      items.push({
        severity: 'info',
        text: t('business.insights.topCustomer', {
          name: overview.topCustomers[0].name,
          count: overview.topCustomers[0].deliveredFills,
        }),
      });
    }

    if (items.length === 0) {
      items.push({ severity: 'info', text: t('business.insights.stable') });
    }

    return items;
  }, [overview, currentSim, simulated, simPrice, t]);

  const handleUnlock = (e: FormEvent) => {
    e.preventDefault();
    if (loginAdmin(adminUser, adminPass)) {
      setUnlocked(true);
      setLoginError(false);
      setAdminUser('');
      setAdminPass('');
    } else {
      setLoginError(true);
    }
  };

  const handleSaveSettings = async () => {
    try {
      const saved = await businessApi.updateSettings({
        refillPriceEur: formPrice,
        sourceCylinderCostEur: formSourceCost,
        sourceCylinderGasKg: formSourceKg,
        consumerCylinderGasG: formConsumerG,
      });
      setSettingsSaved(true);
      setTimeout(() => setSettingsSaved(false), 2500);
      setSimPrice(saved.refillPriceEur);
      setSimSourceCost(saved.sourceCylinderCostEur);
      const refreshed = await businessApi.getOverview(days);
      setOverview(refreshed);
    } catch (err) {
      setError(err instanceof Error ? err.message : t('business.saveError'));
    }
  };

  if (!unlocked) {
    return (
      <div className="max-w-md mx-auto space-y-6 py-12">
        <h1 className="text-2xl font-bold text-center">{t('business.title')}</h1>
        <p className="text-center text-muted-foreground">{t('business.unlockHint')}</p>
        <form onSubmit={handleUnlock} className="p-4 border rounded-lg space-y-4">
          <div className="space-y-2">
            <label className="text-sm font-medium">{t('business.adminUsername')}</label>
            <input
              type="text"
              value={adminUser}
              onChange={(e) => setAdminUser(e.target.value)}
              autoComplete="username"
              className="w-full px-3 py-2 border rounded-lg bg-background"
              autoFocus
              required
            />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium">{t('business.adminPassword')}</label>
            <input
              type="password"
              value={adminPass}
              onChange={(e) => setAdminPass(e.target.value)}
              autoComplete="current-password"
              className="w-full px-3 py-2 border rounded-lg bg-background"
              required
            />
          </div>
          {loginError && (
            <div className="text-sm text-destructive">{t('business.loginInvalid')}</div>
          )}
          <button
            type="submit"
            className="w-full px-4 py-2 bg-primary text-primary-foreground rounded-lg font-medium"
          >
            {t('business.unlock')}
          </button>
        </form>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">{t('business.title')}</h1>
        <div className="flex flex-wrap gap-2">
          {([7, 30, 90, 365] as PeriodDays[]).map((value) => (
            <button
              key={value}
              onClick={() => setDays(value)}
              className={`px-3 py-1.5 rounded-lg text-sm font-medium ${
                days === value
                  ? 'bg-primary text-primary-foreground'
                  : 'border hover:bg-accent'
              }`}
            >
              {t(`business.range.${value}`)}
            </button>
          ))}
        </div>
      </div>

      {error && (
        <div className="p-3 bg-destructive/10 text-destructive rounded-lg text-sm">{error}</div>
      )}

      {loading && !overview && (
        <div className="space-y-4">
          {[...Array(3)].map((_, i) => (
            <div key={i} className="h-28 border rounded-lg animate-pulse bg-muted/50" />
          ))}
        </div>
      )}

      {overview && (
        <>
          {/* KPIs */}
          <section className="space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              {t('business.sections.summary')}
            </h2>
            <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
              <KpiCard
                label={t('business.kpi.revenue')}
                value={formatEur(overview.revenue)}
                delta={formatPct(overview.revenueChangePercent)}
                positive={overview.revenueChangePercent >= 0}
              />
              <KpiCard
                label={t('business.kpi.profit')}
                value={formatEur(overview.grossProfit)}
                delta={formatPct(overview.profitChangePercent)}
                positive={overview.profitChangePercent >= 0}
              />
              <KpiCard
                label={t('business.kpi.margin')}
                value={`${overview.marginPercent.toFixed(1)}%`}
              />
              <KpiCard
                label={t('business.kpi.fills')}
                value={String(overview.fillsDelivered)}
                delta={formatPct(overview.fillsChangePercent)}
                positive={overview.fillsChangePercent >= 0}
              />
              <KpiCard
                label={t('business.kpi.gasCost')}
                value={formatEur(overview.gasCost)}
                sublabel={t('business.kpi.perFill', { value: formatEur(overview.settings.gasCostPerFillEur) })}
              />
              <KpiCard
                label={t('business.kpi.sourceUsed')}
                value={overview.sourceCylindersConsumed.toFixed(2)}
                sublabel={t('business.kpi.produced', { count: overview.fillsProduced })}
              />
            </div>
          </section>

          {/* Chart */}
          <section className="space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              {t('business.sections.evolution')}
            </h2>
            <BusinessChart points={overview.dailySeries} days={days} t={t} />
          </section>

          {/* Simulator */}
          <section className="space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              {t('business.sections.simulator')}
            </h2>
            <div className="border rounded-lg p-4 space-y-4 bg-muted/20">
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <NumberField
                  label={t('business.sim.price')}
                  value={simPrice}
                  step={0.5}
                  onChange={setSimPrice}
                />
                <NumberField
                  label={t('business.sim.volume')}
                  value={simVolume}
                  step={1}
                  onChange={(v) => {
                    setVolumeTouched(true);
                    setSimVolume(v);
                  }}
                />
                <NumberField
                  label={t('business.sim.sourceCost')}
                  value={simSourceCost}
                  step={1}
                  onChange={setSimSourceCost}
                />
              </div>

              <div className="flex flex-wrap gap-2">
                <PresetButton
                  label={t('business.sim.plus1')}
                  onClick={() => setSimPrice(overview.settings.refillPriceEur + 1)}
                />
                <PresetButton
                  label={t('business.sim.plus2')}
                  onClick={() => setSimPrice(overview.settings.refillPriceEur + 2)}
                />
                <PresetButton
                  label={t('business.sim.volUp')}
                  onClick={() => {
                    setVolumeTouched(true);
                    setSimVolume(Math.round(simVolume * 1.2));
                  }}
                />
                <PresetButton
                  label={t('business.sim.volDown')}
                  onClick={() => {
                    setVolumeTouched(true);
                    setSimVolume(Math.max(1, Math.round(simVolume * 0.8)));
                  }}
                />
                <PresetButton
                  label={t('business.sim.reset')}
                  onClick={() => {
                    setSimPrice(overview.settings.refillPriceEur);
                    setSimSourceCost(overview.settings.sourceCylinderCostEur);
                    setSimVolume(Math.max(1, Math.round(overview.averageDailyFills * 30)));
                    setVolumeTouched(false);
                  }}
                />
              </div>

              {currentSim && simulated && (
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b text-left text-muted-foreground">
                        <th className="py-2 pr-4 font-medium">{t('business.sim.metric')}</th>
                        <th className="py-2 pr-4 font-medium">{t('business.sim.current')}</th>
                        <th className="py-2 pr-4 font-medium">{t('business.sim.simulated')}</th>
                        <th className="py-2 font-medium">{t('business.sim.delta')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      <SimRow
                        label={t('business.sim.profitPerFill')}
                        current={formatEur(currentSim.profitPerFill)}
                        simulated={formatEur(simulated.profitPerFill)}
                        delta={formatEur(simulated.profitPerFill - currentSim.profitPerFill)}
                      />
                      <SimRow
                        label={t('business.sim.margin')}
                        current={`${currentSim.margin.toFixed(1)}%`}
                        simulated={`${simulated.margin.toFixed(1)}%`}
                        delta={`${(simulated.margin - currentSim.margin).toFixed(1)} pp`}
                      />
                      <SimRow
                        label={t('business.sim.monthlyRevenue')}
                        current={formatEur(currentSim.monthlyRevenue)}
                        simulated={formatEur(simulated.monthlyRevenue)}
                        delta={formatEur(simulated.monthlyRevenue - currentSim.monthlyRevenue)}
                      />
                      <SimRow
                        label={t('business.sim.monthlyProfit')}
                        current={formatEur(currentSim.monthlyProfit)}
                        simulated={formatEur(simulated.monthlyProfit)}
                        delta={formatEur(simulated.monthlyProfit - currentSim.monthlyProfit)}
                      />
                      <SimRow
                        label={t('business.sim.yearlyProfit')}
                        current={formatEur(currentSim.yearlyProfit)}
                        simulated={formatEur(simulated.yearlyProfit)}
                        delta={formatEur(simulated.yearlyProfit - currentSim.yearlyProfit)}
                      />
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </section>

          {/* Forecast */}
          <section className="space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              {t('business.sections.forecast')}
            </h2>
            <div className="border rounded-lg p-4 space-y-3 bg-muted/20">
              <p className="text-sm text-muted-foreground">
                {t('business.forecast.basis', {
                  avg: overview.averageDailyFills.toFixed(1),
                  days: overview.forecastDays,
                  price: formatEur(simPrice),
                })}
              </p>
              {(() => {
                const forecastFills = overview.averageDailyFills * overview.forecastDays;
                const costPerFill = overview.settings.fillsPerSourceCylinder > 0
                  ? simSourceCost / overview.settings.fillsPerSourceCylinder
                  : 0;
                const forecastRevenue = forecastFills * simPrice;
                const forecastProfit = forecastFills * (simPrice - costPerFill);
                const forecastSources = overview.settings.fillsPerSourceCylinder > 0
                  ? forecastFills / overview.settings.fillsPerSourceCylinder
                  : 0;

                return (
                  <>
                    <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                      <KpiCard label={t('business.forecast.fills')} value={forecastFills.toFixed(0)} />
                      <KpiCard label={t('business.forecast.revenue')} value={formatEur(forecastRevenue)} />
                      <KpiCard label={t('business.forecast.profit')} value={formatEur(forecastProfit)} />
                      <KpiCard label={t('business.forecast.source')} value={forecastSources.toFixed(1)} />
                    </div>
                    <p className="text-sm">
                      {t('business.forecast.summary', {
                        fills: forecastFills.toFixed(0),
                        revenue: formatEur(forecastRevenue),
                        profit: formatEur(forecastProfit),
                        sources: forecastSources.toFixed(1),
                      })}
                    </p>
                  </>
                );
              })()}
              {overview.daysUntilNextSourceCylinder > 0 && (
                <p className="text-sm text-muted-foreground">
                  {t('business.forecast.nextSource', { days: overview.daysUntilNextSourceCylinder })}
                </p>
              )}
            </div>
          </section>

          {/* Insights */}
          <section className="space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              {t('business.sections.insights')}
            </h2>
            <div className="space-y-2">
              {insights.map((item, idx) => (
                <div
                  key={idx}
                  className={`p-3 rounded-lg border text-sm ${
                    item.severity === 'success'
                      ? 'border-green-500/40 bg-green-500/10'
                      : item.severity === 'warning'
                        ? 'border-amber-500/40 bg-amber-500/10'
                        : 'border-border bg-muted/30'
                  }`}
                >
                  {item.text}
                </div>
              ))}
            </div>
          </section>

          {/* Detail: pipeline + top customers + weekday */}
          <section className="space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              {t('business.sections.detail')}
            </h2>
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
              <div className="border rounded-lg p-4 space-y-3">
                <h3 className="font-semibold">{t('business.pipeline.title')}</h3>
                <p className="text-sm text-muted-foreground">{t('business.pipeline.hint')}</p>
                <div className="flex items-end gap-4">
                  <div>
                    <div className="text-3xl font-bold">{overview.pipelineReadyCount}</div>
                    <div className="text-xs text-muted-foreground">{t('business.pipeline.ready')}</div>
                  </div>
                  <div>
                    <div className="text-xl font-semibold">{formatEur(overview.pipelineValue)}</div>
                    <div className="text-xs text-muted-foreground">{t('business.pipeline.value')}</div>
                  </div>
                </div>
              </div>

              <div className="border rounded-lg p-4 space-y-3">
                <h3 className="font-semibold">{t('business.weekday.title')}</h3>
                <div className="space-y-2">
                  {overview.weekdayStats.map((day) => {
                    const max = Math.max(0.1, ...overview.weekdayStats.map((d) => d.averageFills));
                    return (
                      <div key={day.dayOfWeek} className="flex items-center gap-3 text-sm">
                        <div className="w-16 text-muted-foreground">{day.dayName.slice(0, 3)}</div>
                        <div className="flex-1 h-2 rounded-full bg-muted overflow-hidden">
                          <div
                            className="h-full bg-primary/70 rounded-full"
                            style={{ width: `${(day.averageFills / max) * 100}%` }}
                          />
                        </div>
                        <div className="w-10 text-right font-mono text-xs">
                          {day.averageFills.toFixed(1)}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            </div>

            <div className="border rounded-lg p-4 space-y-3">
              <h3 className="font-semibold">{t('business.topCustomers.title')}</h3>
              {overview.topCustomers.length === 0 ? (
                <p className="text-sm text-muted-foreground">{t('business.topCustomers.empty')}</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b text-left text-muted-foreground">
                        <th className="py-2 pr-3 font-medium">#</th>
                        <th className="py-2 pr-3 font-medium">{t('business.topCustomers.name')}</th>
                        <th className="py-2 pr-3 font-medium">{t('business.topCustomers.fills')}</th>
                        <th className="py-2 font-medium">{t('business.topCustomers.revenue')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {overview.topCustomers.map((customer, idx) => (
                        <tr key={customer.customerId} className="border-b border-border/50">
                          <td className="py-2 pr-3 text-muted-foreground">{idx + 1}</td>
                          <td className="py-2 pr-3 font-medium">{customer.name}</td>
                          <td className="py-2 pr-3">{customer.deliveredFills}</td>
                          <td className="py-2">{formatEur(customer.revenue)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </section>

          {/* Settings */}
          <section className="space-y-3">
            <button
              onClick={() => setShowSettings(!showSettings)}
              className="text-sm font-semibold uppercase tracking-wide text-muted-foreground hover:text-foreground"
            >
              {showSettings ? '▾' : '▸'} {t('business.sections.settings')}
            </button>
            {showSettings && (
              <div className="border rounded-lg p-4 space-y-4">
                <p className="text-sm text-muted-foreground">{t('business.settings.hint')}</p>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <NumberField label={t('business.settings.price')} value={formPrice} step={0.5} onChange={setFormPrice} />
                  <NumberField label={t('business.settings.sourceCost')} value={formSourceCost} step={1} onChange={setFormSourceCost} />
                  <NumberField label={t('business.settings.sourceKg')} value={formSourceKg} step={0.1} onChange={setFormSourceKg} />
                  <NumberField label={t('business.settings.consumerG')} value={formConsumerG} step={1} onChange={setFormConsumerG} />
                </div>
                <div className="text-sm text-muted-foreground">
                  {t('business.settings.derived', {
                    fills: (
                      formConsumerG > 0 ? (formSourceKg * 1000) / formConsumerG : 0
                    ).toFixed(1),
                    cost: formatEur(
                      formConsumerG > 0 ? formSourceCost / ((formSourceKg * 1000) / formConsumerG) : 0
                    ),
                  })}
                </div>
                {settingsSaved && (
                  <div className="text-sm text-green-600 dark:text-green-400">{t('business.settings.saved')}</div>
                )}
                <button
                  onClick={() => void handleSaveSettings()}
                  className="px-4 py-2 bg-primary text-primary-foreground rounded-lg font-medium"
                >
                  {t('common.save')}
                </button>
              </div>
            )}
          </section>
        </>
      )}
    </div>
  );
}

function KpiCard({
  label,
  value,
  delta,
  positive,
  sublabel,
}: {
  label: string;
  value: string;
  delta?: string;
  positive?: boolean;
  sublabel?: string;
}) {
  return (
    <div className="p-3 border rounded-lg space-y-1">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="text-xl font-bold tracking-tight">{value}</div>
      {delta && (
        <div className={`text-xs font-medium ${positive ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
          {delta}
        </div>
      )}
      {sublabel && <div className="text-[11px] text-muted-foreground">{sublabel}</div>}
    </div>
  );
}

function NumberField({
  label,
  value,
  step,
  onChange,
}: {
  label: string;
  value: number;
  step: number;
  onChange: (v: number) => void;
}) {
  return (
    <div className="space-y-1">
      <label className="text-sm font-medium">{label}</label>
      <input
        type="number"
        step={step}
        value={value}
        onChange={(e) => onChange(Number(e.target.value) || 0)}
        className="w-full px-3 py-2 border rounded-lg bg-background"
      />
    </div>
  );
}

function PresetButton({ label, onClick }: { label: string; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="px-3 py-1.5 text-xs border rounded-lg hover:bg-accent"
    >
      {label}
    </button>
  );
}

function SimRow({
  label,
  current,
  simulated,
  delta,
}: {
  label: string;
  current: string;
  simulated: string;
  delta: string;
}) {
  return (
    <tr className="border-b border-border/40">
      <td className="py-2 pr-4">{label}</td>
      <td className="py-2 pr-4 font-mono text-xs">{current}</td>
      <td className="py-2 pr-4 font-mono text-xs font-semibold">{simulated}</td>
      <td className="py-2 font-mono text-xs text-muted-foreground">{delta}</td>
    </tr>
  );
}

function BusinessChart({
  points,
  days,
  t,
}: {
  points: BusinessOverview['dailySeries'];
  days: number;
  t: ReturnType<typeof useTranslations>;
}) {
  const width = 640;
  const height = 240;
  const padding = { top: 16, right: 12, bottom: 28, left: 40 };
  const innerWidth = width - padding.left - padding.right;
  const innerHeight = height - padding.top - padding.bottom;
  const maxValue = Math.max(1, ...points.flatMap((p) => [p.revenue, Math.max(0, p.profit), p.delivered]));

  const toX = (index: number) =>
    padding.left + (points.length <= 1 ? innerWidth / 2 : (index * innerWidth) / (points.length - 1));
  const toY = (value: number) =>
    padding.top + innerHeight - (Math.max(0, value) / maxValue) * innerHeight;

  const buildPath = (values: number[]) =>
    values.map((value, index) => `${index === 0 ? 'M' : 'L'} ${toX(index)} ${toY(value)}`).join(' ');

  const series = [
    { key: 'revenue', label: t('business.chart.revenue'), color: '#2563eb', values: points.map((p) => p.revenue) },
    { key: 'profit', label: t('business.chart.profit'), color: '#16a34a', values: points.map((p) => Math.max(0, p.profit)) },
  ] as const;

  return (
    <div className="border rounded-lg bg-muted/20 p-4 space-y-3">
      <div className="flex flex-wrap gap-3 text-sm">
        {series.map((item) => (
          <div key={item.key} className="flex items-center gap-2 text-muted-foreground">
            <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ backgroundColor: item.color }} />
            <span>{item.label}</span>
          </div>
        ))}
      </div>
      <svg viewBox={`0 0 ${width} ${height}`} className="w-full h-auto">
        {[0, 0.5, 1].map((ratio) => {
          const value = Math.round(maxValue * ratio);
          const y = toY(value);
          return (
            <g key={ratio}>
              <line x1={padding.left} x2={width - padding.right} y1={y} y2={y} stroke="currentColor" strokeOpacity="0.12" />
              <text x={padding.left - 6} y={y + 3} textAnchor="end" className="fill-muted-foreground text-[10px]">
                {value}
              </text>
            </g>
          );
        })}
        {series.map((item) => (
          <path
            key={item.key}
            d={buildPath(item.values)}
            fill="none"
            stroke={item.color}
            strokeWidth="2.5"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        ))}
        {points.map((point, index) => {
          const step = days <= 7 ? 1 : days <= 30 ? 5 : days <= 90 ? 10 : 30;
          if (index !== 0 && index !== points.length - 1 && index % step !== 0) return null;
          return (
            <text
              key={point.date}
              x={toX(index)}
              y={height - 8}
              textAnchor="middle"
              className="fill-muted-foreground text-[10px]"
            >
              {formatShortDate(point.date)}
            </text>
          );
        })}
      </svg>
    </div>
  );
}
