import { useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr';
import type { ArtifactMetadata, CapabilityDescriptor, ConnectionSnapshot, DriverRegistration, LiveValueEvent, OperationJob, ParameterSample, ProgressEvent, Recording, ReviewResponse } from './contracts';
import { api } from './api';

const sections = ['Guided', 'Diagnostics', 'Live Data', 'Configuration', 'Programming', 'Service Functions', 'Repair Guide'] as const;
type Section = typeof sections[number];
const categoryFor: Record<Section, string[]> = {
  Guided: [], Diagnostics: ['Vehicle & interface', 'Diagnostics'], 'Live Data': ['Live data'],
  Configuration: ['Configuration', 'Retrofits'], Programming: ['Programming', 'Recovery', 'Replacement modules'],
  'Service Functions': ['Service functions', 'Security access', 'Keys & mileage/security'], 'Repair Guide': ['Fault & repair guide', 'Remote assistance'],
};
const steps = ['Environment', 'Driver health', 'Interface', 'Vehicle identity', 'Decoder prerequisites'];
const labels: Record<string, string> = { unknown: 'Unknown', unsupported: 'Unsupported', blocked: 'Blocked', implementedUnverified: 'Implemented · unverified', hardwareVerified: 'Hardware verified', equipmentNonApplicable: 'Equipment non-applicable' };

export default function App({ bootstrap }: { bootstrap: Promise<void> }) {
  const [section, setSection] = useState<Section>('Guided');
  const [state, setState] = useState<ConnectionSnapshot>();
  const [features, setFeatures] = useState<CapabilityDescriptor[]>([]);
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  const [driver, setDriver] = useState(''); const [jobs, setJobs] = useState<OperationJob[]>([]);
  const [recordings, setRecordings] = useState<Recording[]>([]); const [selected, setSelected] = useState<Recording>();
  const [artifacts, setArtifacts] = useState<ArtifactMetadata[]>([]); const [events, setEvents] = useState<ProgressEvent[]>([]);
  const [review, setReview] = useState<ReviewResponse>(); const [note, setNote] = useState('Connection infrastructure check');
  const [search, setSearch] = useState(''); const [favourite, setFavourite] = useState(false);
  const [threshold, setThreshold] = useState(12.5); const [replay, setReplay] = useState<number | null>(null);
  const [source, setSource] = useState(''); const [licence, setLicence] = useState(''); const [applicability, setApplicability] = useState(''); const [kind, setKind] = useState('definitions');
  const [file, setFile] = useState<File>(); const [live, setLive] = useState<ParameterSample>();
  const [hub, setHub] = useState('Offline');
  const environment = useRef<ConnectionSnapshot['environment']>('hardware');

  async function refresh() {
    const [status, capabilities, operations, captures, imports] = await Promise.all([
      api<ConnectionSnapshot>('status'), api<CapabilityDescriptor[]>('features'), api<OperationJob[]>('operations'), api<Recording[]>('recordings'), api<ArtifactMetadata[]>('artifacts'),
    ]);
    if (status.environment !== environment.current) {
      setEvents([]); setLive(undefined); setSelected(undefined); setReview(undefined); setReplay(null); setFavourite(false); setThreshold(12.5);
    }
    environment.current = status.environment;
    setState(status); setFeatures(capabilities); setJobs(operations.filter(j => j.context.environment === status.environment)); setRecordings(captures.filter(r => r.context.environment === status.environment)); setArtifacts(imports.filter(a => a.context.environment === status.environment));
    return status;
  }
  async function run(action: () => Promise<unknown>) {
    setBusy(true); setError(''); try { await action(); await refresh(); } catch (e) { setError(e instanceof Error ? e.message : 'Local request failed.'); } finally { setBusy(false); }
  }
  useEffect(() => {
    let cancelled = false;
    const connection = new HubConnectionBuilder().withUrl('/hubs/progress', { transport: HttpTransportType.WebSockets, skipNegotiation: true }).configureLogging(LogLevel.None).build();
    connection.on('progress', (event: ProgressEvent) => { if (event.context.environment === environment.current) setEvents(previous => [event, ...previous].slice(0, 15)); });
    connection.on('live', (event: LiveValueEvent) => { if (event.context.environment === environment.current) setLive(event.sample); });
    connection.onclose(() => setHub('Offline'));
    void bootstrap.then(async () => { if (cancelled) return; await refresh(); if (!cancelled) { await connection.start(); setHub('Connected'); } }).catch(e => { if (!cancelled) setError(e.message); });
    return () => { cancelled = true; void connection.stop(); };
  }, [bootstrap]);
  useEffect(() => { if (!selected || replay === null) return; const timer = setInterval(() => setReplay(v => v === null || v >= selected.samples.length - 1 ? null : v + 1), 200); return () => clearInterval(timer); }, [selected, replay]);
  const simulation = state?.environment === 'simulation'; const connected = state?.phase === 'vehicleConfirmed' || state?.phase === 'interfaceOpen';
  const displayFeatures = features.filter(f => (section === 'Guided' || categoryFor[section].includes(f.category)) && `${f.title} ${f.category}`.toLowerCase().includes(search.toLowerCase()));
  const currentSample = selected?.samples[replay ?? ((selected?.samples.length ?? 1) - 1)];

  async function importArtifact() {
    if (!file || file.size > 4 * 1024 * 1024) throw new Error('Select an artifact up to 4 MiB. Larger production packs need a documented streaming importer.');
    const bytes = new Uint8Array(await file.arrayBuffer());
    const hash = [...new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))].map(x => x.toString(16).padStart(2, '0')).join('');
    let binary = ''; for (const byte of bytes) binary += String.fromCharCode(byte);
    await api('artifacts', { contentBase64: btoa(binary), source, licence, moduleApplicability: applicability.split(',').map(s => s.trim()).filter(Boolean), expectedSha256: hash, kind });
    setFile(undefined);
  }
  return <div className="shell">
    <aside className="sidebar"><a className="brand" href="/" aria-label="Nordlink home"><span className="brand-icon">N</span><span>NORDLINK<small>LOCAL VEHICLE WORKSPACE</small></span></a>
      <div className="nav-label">WORKSPACE</div><nav aria-label="Main navigation">{sections.map((item, index) => <button key={item} aria-label={item} className={section === item ? 'active' : ''} onClick={() => { setSection(item); setSearch(''); }}><span className="nav-icon">{['◈', '⌕', '∿', '⚙', '▤', '◇', '▧'][index]}</span>{item}{item === 'Guided' && <span className="nav-pill">START</span>}</button>)}</nav>
      <div className="sidebar-bottom"><span className="local-dot" /> Local by design<p>Data stays on this computer.<br />Hardware execution requires Windows.</p><span className="version">FOUNDATION / 0.1.0</span></div>
    </aside>
    <div className="workspace"><header><div className="breadcrumb">Workspace <span>/</span> <strong>{section}</strong></div><div className="header-status"><span className={connected ? 'dot teal' : 'dot'} />{state?.phase === 'interfaceOpen' ? 'Interface open · vehicle unverified' : connected ? 'Simulation fixture connected' : 'Disconnected'}<span className="private-label">127.0.0.1 only</span></div></header>
      <main>
        <div className="eyebrow">NORDLINK / {section.toUpperCase()}</div><div className="page-title"><div><h1>{section === 'Guided' ? 'Start with a trusted connection.' : section}</h1><p>{section === 'Guided' ? 'A clear path from interface health to verified vehicle identity.' : 'Every operation follows the evidence available for this vehicle.'}</p></div><span className="tag">V40 FIRST</span></div>
        {error && <div role="alert" className="error"><strong>Request needs attention</strong><p>{error}</p></div>}
        {simulation && <div className="simulation-banner"><strong>SIMULATION ENVIRONMENT</strong><span>Demonstration data is isolated. It never counts as hardware evidence.</span></div>}
        {!state && <section className="card"><h2>Open a private launch session</h2><p>Use the launch URL created by the gateway. The fragment is removed immediately, and the session stays in an HttpOnly cookie.</p></section>}
        {state && <>
          <section className="vehicle-strip"><div className="vehicle-symbol">V40<span>D2</span></div><div><small>INTENDED VEHICLE · UNVERIFIED</small><h2>2013 Volvo V40 D2</h2><p>{state.vehicle ? state.vehicle.model : 'Connect and identify before using vehicle functions.'}</p></div><div className="identity"><small>MEASURED VIN</small><strong>{state.vehicle?.vin ?? 'Not identified'}</strong></div><div className="identity"><small>MEASURED MODULES</small><strong>{state.vehicle ? `${state.vehicle.modules.length} simulation fixture` : 'Not identified'}</strong></div></section>
          {section === 'Guided' && <>
            <div className="steps" aria-label="Connection steps">{steps.map((step, index) => <div key={step} className={(index === 0 || connected && index < 3 ? 'done' : '')}><span>{index + 1}</span>{step}</div>)}</div>
            <div className="two-column"><section className="card"><div className="card-kicker">01 / ENVIRONMENT & INTERFACE</div><h2>Choose your workspace</h2><p>Hardware uses the official TOPDON driver. Simulation uses an explicit development fixture.</p>
              <div className="segmented" role="group" aria-label="Environment"><button aria-pressed={!simulation} disabled={busy || state.phase !== 'disconnected'} onClick={() => void run(async () => { await api('environment', { environment: 'hardware' }); setSelected(undefined); setReview(undefined); })}>Hardware <small>Windows + TOPDON</small></button><button aria-pressed={simulation} disabled={busy || state.phase !== 'disconnected'} onClick={() => void run(async () => { await api('environment', { environment: 'simulation' }); setSelected(undefined); setReview(undefined); })}>Simulation <small>Isolated demo data</small></button></div>
              <div className="actions"><button className="secondary" disabled={busy || state.phase !== 'disconnected'} onClick={() => void run(() => api('drivers/inspect', {}))}>Inspect driver health</button><button className="primary" disabled={busy || state.phase !== 'disconnected' || !simulation && !driver} onClick={() => void run(() => api('connect', { driverPath: driver || null }))}>{busy ? 'Working…' : simulation ? 'Connect simulation' : 'Open interface'} <span>→</span></button>{state.phase !== 'disconnected' && <button className="secondary" disabled={busy} onClick={() => void run(async () => { await api('disconnect', {}); setReview(undefined); })}>Disconnect</button>}</div>
              {state.drivers.length > 0 && <label className="field">Inspected TOPDON registration<select value={driver} onChange={e => setDriver(e.target.value)}><option value="">Select a library</option>{state.drivers.map((d: DriverRegistration) => <option key={d.registryView + d.path} value={d.path} disabled={!d.exists || !!d.problem}>{d.name} · {d.architecture} · {d.exists ? d.fileVersion : 'missing DLL'}</option>)}</select></label>}
              {state.drivers.map(d => <div className="driver" key={d.registryView + d.path}><strong>{d.name} / {d.registryView}</strong><code>{d.path}</code><p>{d.architecture} · Version {d.fileVersion ?? 'unknown'} · Publisher {d.publisher ?? 'unknown'}</p>{d.problem && <p>{d.problem}</p>}</div>)}
              {state.versions && <div className="driver"><strong>Measured PassThruReadVersion</strong><p>Driver {state.versions.driver} · Firmware {state.versions.firmware} · API {state.versions.api}</p><p>{new Date(state.versions.context.observedAt).toLocaleString()} · {state.versions.context.source}</p></div>}<div className="notice"><strong>Connection evidence</strong><ul>{state.blockers.map(b => <li key={b}>{b}</li>)}</ul></div>
            </section><section className="card"><div className="card-kicker">02 / READINESS</div><h2>Know what is confirmed</h2><div className="readiness"><Readiness label="Local secure session" detail="Same-origin session established" ok /><Readiness label="Interface availability" detail={state.interfaceAvailable ? (simulation ? 'Simulation fixture only' : 'PassThruOpen + version checks completed') : 'No successful interface open'} ok={state.interfaceAvailable} /><Readiness label="Vehicle communication" detail={state.vehicleCommunicationConfirmed ? 'Simulation fixture only; no real vehicle evidence' : 'Real VIN, CEM and ECM remain unknown'} ok={state.vehicleCommunicationConfirmed} /><Readiness label="Reusable CEM PIN decoder" detail="Applicable algorithm and independent verification missing" ok={false} /><Readiness label="Vehicle write prerequisites" detail="Backups, restoration and validated procedures required" ok={false} /></div><div className="quiet-note">An open interface does not confirm vehicle communication. Missing definitions remain blockers.</div></section></div>
            {simulation && connected && <section className="card exercise"><div><div className="card-kicker">FOUNDATION VALIDATION / NO VEHICLE WRITE</div><h2>Review → execute → record</h2><p>Exercise approval binding and job progress using the connected simulation fixture.</p></div><label className="field">Non-sensitive fixture note<input value={note} maxLength={120} onChange={e => { setNote(e.target.value); setReview(undefined); }} /></label><button className="secondary" disabled={busy} onClick={() => void run(async () => setReview(await api<ReviewResponse>('operations/review', { capability: 'simulation.exercise', vehicleId: state.vehicle!.id, moduleId: state.vehicle!.modules[0].id, inputs: { note } })))}>Review exercise</button>{review && <div className="review"><strong>Reviewed summary</strong><p>{review.job.summary}</p><p>Approval expires {new Date(review.job.expiresAt).toLocaleTimeString()} and becomes invalid after connection changes.</p><button className="primary" disabled={busy} onClick={() => void run(async () => { await api(`operations/${review.job.id}/execute`, { approvalToken: review.approvalToken }); setReview(undefined); })}>Execute reviewed exercise</button></div>}</section>}
          </>}
          {section === 'Live Data' && <section className="card"><div className="section-heading"><div><div className="card-kicker">RECORDING & REPLAY</div><h2>Preserve values, time and gaps</h2><p>Real parameter discovery and scaling require licensed definitions. The explicit fixture demonstrates local recording and replay.</p></div><button className="primary" disabled={busy || !simulation || !connected} onClick={() => void run(async () => { const r = await api<Recording>('recordings/simulation-fixture', {}); setSelected(r); })}>Create fixture recording</button></div>
            <label className="field">Saved recordings<select value={selected?.id ?? ''} onChange={e => { setSelected(recordings.find(r => r.id === e.target.value)); setReplay(null); }}><option value="">Select a recording</option>{recordings.map(r => <option key={r.id} value={r.id}>{new Date(r.context.observedAt).toLocaleString()} · {r.samples.length} samples · {r.context.environment}</option>)}</select></label>
            {selected && <><div className="live-grid"><div className="gauge"><small>FIXTURE VOLTAGE</small><strong>{currentSample?.gap ? 'Gap' : currentSample?.value?.toFixed(3)} <span>{currentSample?.unit}</span></strong><p>{currentSample && new Date(currentSample.timestamp).toLocaleTimeString()}</p><label><input type="checkbox" checked={favourite} onChange={e => setFavourite(e.target.checked)} /> Favourite parameter</label></div><div className="chart"><Chart samples={selected.samples.slice(0, replay === null ? undefined : replay + 1)} /><label>Upper threshold (V) <input type="number" step="0.1" value={threshold} onChange={e => setThreshold(Number(e.target.value))} /></label><p className={currentSample?.value != null && currentSample.value > threshold ? 'threshold-alert' : ''}>{currentSample?.value != null && currentSample.value > threshold ? 'Threshold exceeded in this fixture sample' : 'No upper threshold alert for this sample'}</p></div></div><div className="actions"><button className="secondary" onClick={() => setReplay(replay === null ? 0 : null)}>{replay === null ? 'Replay recording' : 'Stop replay'}</button><a className="button secondary" href={`/api/recordings/${selected.id}/csv`} download>Download CSV</a><span className="muted">Gaps are preserved as empty values.</span></div><div className="table-scroll"><table><thead><tr><th>Timestamp</th><th>Parameter</th><th>Value</th><th>Unit</th><th>Gap</th></tr></thead><tbody>{selected.samples.map((s, i) => <tr key={i}><td>{s.timestamp}</td><td>{s.parameterId}</td><td>{s.value ?? '—'}</td><td>{s.unit}</td><td>{s.gap ? 'Yes' : 'No'}</td></tr>)}</tbody></table></div></>}
            {simulation && connected && <div className="actions"><button className="secondary" disabled={busy} onClick={() => void run(() => api('live/simulation/start', {}))}>Start fixture stream</button><button className="secondary" disabled={busy} onClick={() => void run(() => api('live/simulation/stop', {}))}>Stop fixture stream</button></div>}
            {live && <p>Latest simulation stream sample: {live.value ?? 'gap'} {live.unit}</p>}
          </section>}
          {section === 'Configuration' && <section className="card"><div className="card-kicker">LOCAL ARTIFACT INTAKE</div><h2>Import with provenance</h2><p>Stores a hash-checked local artifact and metadata. A file import does not enable any vehicle procedure or validate its proprietary format.</p><div className="form-grid"><label className="field">Artifact kind<select value={kind} onChange={e => setKind(e.target.value)}>{['definitions', 'firmware', 'repair', 'configuration'].map(k => <option key={k}>{k}</option>)}</select></label><label className="field">Source<input value={source} onChange={e => setSource(e.target.value)} placeholder="Licensed supplier / source document" /></label><label className="field">Licence / permission reference<input value={licence} onChange={e => setLicence(e.target.value)} /></label><label className="field">Exact module applicability<input value={applicability} onChange={e => setApplicability(e.target.value)} placeholder="Part / software identifiers, comma separated" /></label><label className="field">Local file (up to 4 MiB)<input type="file" onChange={e => setFile(e.target.files?.[0])} /></label></div><button className="secondary" disabled={busy || !file || !source || !licence || !applicability} onClick={() => void run(importArtifact)}>Validate hash & import</button>{artifacts.map(a => <div className="artifact" key={a.sha256}><strong>{a.kind} · {a.source}</strong><code>{a.sha256}</code><p>{a.licence} · {a.size} bytes · {a.context.environment}</p></div>)}</section>}
          <section className="card"><div className="section-heading"><div><div className="card-kicker">CAPABILITY REGISTER</div><h2>{section === 'Guided' ? 'Evidence before capability.' : `${section} readiness`}</h2><p>{features.length} tracked operations across 13 categories. Hardware acceptance evidence is currently empty.</p></div><label className="search">Find capability<input aria-label="Find capability" value={search} onChange={e => setSearch(e.target.value)} placeholder="Search operations…" /></label></div><div className="capabilities">{displayFeatures.map(f => <article className="capability" key={f.id}><div><small>{f.category}</small><h3>{f.title}</h3></div><span className={`status ${f.state}`}>{labels[f.state]}</span><p>{f.dependencies.join(' · ')}</p><details><summary>Acceptance & applicability</summary><p>{f.acceptanceTest}</p><p>Modules: {f.moduleApplicability.join(', ')}</p><p>Implementation: {f.implementationStatus}</p><p>Implementation checks: {f.implementationEvidence.join(", ") || "None recorded"}</p><p>Source: {f.sourceClaim}</p><p>Evidence: {f.evidence.length ? f.evidence.join(', ') : 'None recorded'}</p></details></article>)}</div></section>
          <section className="card"><div className="section-heading"><div><div className="card-kicker">LOCAL HISTORY</div><h2>Operations & progress</h2></div><span className="muted">SignalR: {hub}</span></div>{jobs.length === 0 && <p>No operations recorded in this environment.</p>}{jobs.map(j => <div className="history-row" key={j.id}><div><strong>{j.capability}</strong><p>{j.summary}</p><small>{j.context.environment} · {new Date(j.createdAt).toLocaleString()}</small></div><span className="status">{j.state}</span></div>)}{events.map((e, i) => <p className="event" key={i}>{e.message}</p>)}</section>
        </>}
        <footer><span>NORDLINK · PERSONAL LOCAL WORKSPACE</span><span>Foundation under validation · No full-parity claim</span></footer>
      </main>
    </div>
  </div>;
}

function Readiness({ label, detail, ok }: { label: string; detail: string; ok: boolean }) { return <div className="readiness-row"><span className={ok ? 'check confirmed' : 'check'}>{ok ? '✓' : '·'}</span><div><strong>{label}</strong><p>{detail}</p></div></div>; }
function Chart({ samples }: { samples: ParameterSample[] }) {
  const groups: string[][] = [[]];
  samples.forEach((s, i) => { if (s.gap || s.value === null) groups.push([]); else groups[groups.length - 1].push(`${20 + i * 20},${135 - (s.value - 12.1) * 190}`); });
  return <svg viewBox="0 0 520 160" role="img" aria-label="Recorded fixture voltage graph; breaks represent gaps"><line x1="20" y1="130" x2="510" y2="130" className="gridline" /><line x1="20" y1="70" x2="510" y2="70" className="gridline" />{groups.filter(g => g.length).map((g, i) => <polyline key={i} points={g.join(' ')} fill="none" className="trace" />)}<text x="20" y="155">Timestamp →</text><text x="400" y="155">SIMULATION / V</text></svg>;
}
