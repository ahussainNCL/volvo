import { useEffect, useState } from 'react';
import App from './App';
import { api } from './api';
import type { PairingCode } from './contracts';

const ready = Promise.resolve();
export default function CloudWorkspace() {
  const [signedIn, setSignedIn] = useState(false); const [connected, setConnected] = useState(false);
  const [password, setPassword] = useState(''); const [pairing, setPairing] = useState<PairingCode>();
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  async function status() {
    const result = await api<{ authenticated: boolean; helperConnected: boolean }>('cloud/session');
    setSignedIn(result.authenticated); setConnected(result.helperConnected);
  }
  useEffect(() => {
    void status().catch(() => setSignedIn(false));
    const timer = setInterval(() => { void status().catch(() => { setSignedIn(false); setConnected(false); }); }, 3000);
    return () => clearInterval(timer);
  }, []);
  async function run(action: () => Promise<void>) {
    setBusy(true); setError(''); try { await action(); } catch (e) { setError(e instanceof Error ? e.message : 'Request failed.'); } finally { setBusy(false); }
  }
  if (signedIn && connected) return <><div className="cloud-toolbar"><strong>NORDLINK ONLINE</strong><span>Connected to your helper PC · vehicle readiness shown below</span><button onClick={() => void run(async () => { await api('cloud/unpair', {}); setConnected(false); setPairing(undefined); })}>Unpair helper</button><button onClick={() => void run(async () => { await api('cloud/logout', {}); setSignedIn(false); setConnected(false); setPairing(undefined); })}>Sign out</button></div><App bootstrap={ready} cloud /></>;
  return <main className="cloud-welcome"><div className="eyebrow">NORDLINK / ONLINE WORKSPACE</div><h1>Your Volvo workspace.<br />Connected to your own PC.</h1><p>Diagnostics, live data, configuration and programming are tracked against real vehicle evidence. Your Windows helper operates the TOPDON interface; credentials remain local.</p>
    {error && <div role="alert" className="error">{error}</div>}
    <section className="card">{!signedIn ? <form onSubmit={e => { e.preventDefault(); void run(async () => { await api('cloud/login', { password }); setPassword(''); await status(); }); }}><h2>Sign in to your personal workspace</h2><label className="field">Workspace password<input aria-label="Workspace password" type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required /></label><button className="primary" disabled={busy}>Sign in</button><p>The password is configured securely by you on the host. Never enter a vehicle PIN here.</p></form> : <><div className="card-kicker">STEP 1 / CONNECT THE WINDOWS HELPER</div><h2>Pair this browser with your PC</h2><p>Connect TOPDON to the Windows PC and run the Nordlink helper. Enter the code shown here into the helper's terminal. No port forwarding is needed.</p><button className="primary" disabled={busy} onClick={() => void run(async () => setPairing(await api<PairingCode>('cloud/pairing', {})))}>Create pairing code</button>{pairing && <div className="review"><strong className="pairing-code">{pairing.code}</strong><p>One use · expires {new Date(pairing.expiresAt).toLocaleTimeString()}. Keep this code private.</p></div>}<label className="field">Helper command (run on Windows)<code>Nordlink.Helper.exe --cloud {location.origin}</code></label><div className="actions"><button className="secondary" onClick={() => void run(status)}>Check connection</button><button className="secondary" onClick={() => void run(async () => { await api('cloud/logout', {}); setSignedIn(false); setPairing(undefined); })}>Sign out</button></div></>}</section>
    <section className="card"><h2>What is available</h2><p>The software foundation provides connection management, isolated simulation, recordings, replay and reviewed fixture operations. Real V40 diagnostics, the reusable CEM decoder and vehicle writes need the hardware evidence and licensed definitions listed in the capability register.</p><p>Sessions and pairing expire on cloud restart. Vehicle files and DPAPI credentials stay on the helper PC. Remote artifact imports and credential reveal are unavailable.</p></section></main>;
}
