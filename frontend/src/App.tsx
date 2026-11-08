import { useCallback, useEffect, useRef, useState } from "react"; // hooks
import * as api from "./api"; // API
import type { ClassItem, HoldItem } from "./api"; // types

const SID_RE = /^\d{17}$/; // 17 ongko
const STATUS_BN: Record<string, string> = { HELD: "চলমান (HELD)", CONFIRMED: "ভর্তি নিশ্চিত (CONFIRMED)", EXPIRED: "মেয়াদ শেষ (EXPIRED)", CANCELLED: "বাতিল (CANCELLED)" }; // status label
const ts = (s: string) => Date.parse(s.replace(/(\.\d{3})\d+/, "$1")); // 7 digit fraction 3 digit-e kati
const fmt = (sec: number) => `${String(Math.floor(sec / 60)).padStart(2, "0")}:${String(sec % 60).padStart(2, "0")}`; // mm:ss

export default function App() { // main component
  const [savedId, setSavedId] = useState(localStorage.getItem("studentId") ?? ""); // refresh-e id thake
  const [draft, setDraft] = useState(savedId); // input
  const [classes, setClasses] = useState<ClassItem[] | null>(null); // class list
  const [holds, setHolds] = useState<HoldItem[] | null>(null); // abedon list
  const [classErr, setClassErr] = useState(""); // class load error
  const [holdErr, setHoldErr] = useState(""); // hold load error
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null); // success/error banner
  const [busy, setBusy] = useState(""); // kon action cholche
  const [now, setNow] = useState(Date.now()); // countdown-er jonno
  const [name, setName] = useState(""); // notun class nam
  const [seats, setSeats] = useState(""); // notun class seat
  const offset = useRef(0); // server time - browser time
  const idOk = SID_RE.test(savedId); // id thik?

  const loadClasses = useCallback(async () => { // class load
    try { setClasses(await api.listClasses()); setClassErr(""); } // success
    catch (e) { setClassErr((e as Error).message); } // error
  }, []);

  const loadHolds = useCallback(async () => { // abedon load
    if (!SID_RE.test(savedId)) { setHolds(null); return; } // id na thakle kichu na
    try { // try
      const h = await api.myHolds(savedId); // fetch
      if (h.length > 0) offset.current = ts(h[0].serverTime) - Date.now(); // clock difference
      setHolds(h); setHoldErr(""); // save
    } catch (e) { setHoldErr((e as Error).message); } // error
  }, [savedId]);

  useEffect(() => { loadClasses(); const t = setInterval(loadClasses, 3000); return () => clearInterval(t); }, [loadClasses]); // 3s por por seat update
  useEffect(() => { loadHolds(); const t = setInterval(loadHolds, 3000); return () => clearInterval(t); }, [loadHolds]); // 3s por por abedon update
  useEffect(() => { const t = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(t); }, []); // 1s por por clock

  const view = (h: HoldItem) => { // meyad hishab
    const left = Math.max(0, Math.ceil((ts(h.expiresAt) - (now + offset.current)) / 1000)); // baki second
    return { status: h.status === "HELD" && left <= 0 ? "EXPIRED" : h.status, left }; // 0 hole EXPIRED
  };

  async function run(key: string, fn: () => Promise<void>, okText: string) { // action helper
    setBusy(key); setMsg(null); // shuru
    try { await fn(); setMsg({ ok: true, text: okText }); } // success
    catch (e) { setMsg({ ok: false, text: (e as Error).message }); } // error
    finally { setBusy(""); await Promise.all([loadClasses(), loadHolds()]); } // refresh
  }

  function saveId(e: React.FormEvent) { // id save
    e.preventDefault(); // reload off
    const v = draft.trim(); // trim
    if (!SID_RE.test(v)) { setMsg({ ok: false, text: "জন্ম নিবন্ধন নম্বর ঠিক ১৭ অঙ্কের হতে হবে।" }); return; } // vul
    localStorage.setItem("studentId", v); setSavedId(v); // save
    setMsg({ ok: true, text: "শিক্ষার্থীর পরিচয় সংরক্ষণ করা হয়েছে।" }); // message
  }

  function addClass(e: React.FormEvent) { // class toiri
    e.preventDefault(); // reload off
    const n = Number(seats); // seat number
    if (!name.trim()) { setMsg({ ok: false, text: "ক্লাসের নাম দিন।" }); return; } // nam
    if (!Number.isInteger(n) || n < 1) { setMsg({ ok: false, text: "আসন সংখ্যা ১ বা তার বেশি পূর্ণসংখ্যা হতে হবে।" }); return; } // seat
    run("class", async () => { await api.createClass(name.trim(), n); setName(""); setSeats(""); }, "ক্লাস তৈরি হয়েছে।"); // call
  }

  const active = new Set((holds ?? []).filter((h) => ["HELD", "CONFIRMED"].includes(view(h).status)).map((h) => h.classId)); // jei class-e abedon ache

  return (
    <div className="wrap">
      <h1>স্কুল ভর্তি ব্যবস্থা</h1>
      {msg && <div className={msg.ok ? "msg ok" : "msg err"} role="alert">{msg.text}</div>}

      {/* শিক্ষার্থীর পরিচয় */}
      <section>
        <h2>শিক্ষার্থীর পরিচয়</h2>
        <form onSubmit={saveId} className="row">
          <input value={draft} onChange={(e) => setDraft(e.target.value)} placeholder="১৭ অঙ্কের জন্ম নিবন্ধন নম্বর" inputMode="numeric" maxLength={17} aria-label="জন্ম নিবন্ধন নম্বর" />
          <button type="submit">নিশ্চিত করুন</button>
        </form>
        <p className="hint">{idOk ? <>বর্তমান শিক্ষার্থী: <b>{savedId}</b></> : "আবেদন করতে আগে জন্ম নিবন্ধন নম্বর দিন।"}</p>
      </section>

      {/* ক্লাস তৈরি (স্কুল অ্যাডমিন) */}
      <section>
        <h2>নতুন ক্লাস তৈরি (স্কুল অ্যাডমিন)</h2>
        <form onSubmit={addClass} className="row">
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="যেমন: Class 6 - Morning Shift" maxLength={100} aria-label="ক্লাসের নাম" />
          <input value={seats} onChange={(e) => setSeats(e.target.value)} placeholder="মোট আসন" inputMode="numeric" aria-label="মোট আসন" className="small" />
          <button type="submit" disabled={busy !== ""}>{busy === "class" ? "অপেক্ষা করুন…" : "তৈরি করুন"}</button>
        </form>
      </section>

      {/* ক্লাস তালিকা */}
      <section>
        <h2>ক্লাস তালিকা</h2>
        {classErr && <div className="msg err">{classErr}</div>}
        {!classes && !classErr && <p className="hint">লোড হচ্ছে…</p>}
        {classes && classes.length === 0 && <p className="hint">এখনও কোনো ক্লাস নেই। উপরে নতুন ক্লাস তৈরি করুন।</p>}
        {classes && classes.length > 0 && (
          <div className="scroll">
            <table>
              <thead><tr><th>ক্লাস</th><th>মোট</th><th>hold-এ</th><th>নিশ্চিত</th><th>খালি</th><th></th></tr></thead>
              <tbody>
                {classes.map((c) => (
                  <tr key={c.id}>
                    <td>{c.name}</td><td>{c.totalSeats}</td><td>{c.heldSeats}</td><td>{c.confirmedSeats}</td><td><b>{c.availableSeats}</b></td>
                    <td>
                      <button
                        disabled={!idOk || busy !== "" || active.has(c.id) || c.availableSeats <= 0}
                        onClick={() => run("apply" + c.id, async () => { await api.createHold(c.id, savedId); }, "আবেদন সফল হয়েছে। মেয়াদ শেষ হওয়ার আগে ভর্তি ফি দিন।")}
                      >
                        {active.has(c.id) ? "আবেদন করা হয়েছে" : c.availableSeats <= 0 ? "আসন নেই" : busy === "apply" + c.id ? "অপেক্ষা করুন…" : "আবেদন করুন"}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* আমার আবেদন */}
      <section>
        <h2>আমার আবেদন</h2>
        {!idOk && <p className="hint">আবেদন দেখতে উপরে জন্ম নিবন্ধন নম্বর দিন।</p>}
        {idOk && holdErr && <div className="msg err">{holdErr}</div>}
        {idOk && !holds && !holdErr && <p className="hint">লোড হচ্ছে…</p>}
        {idOk && holds && holds.length === 0 && <p className="hint">এই শিক্ষার্থীর এখনও কোনো আবেদন নেই।</p>}
        {idOk && holds && holds.length > 0 && (
          <div className="scroll">
            <table>
              <thead><tr><th>ক্লাস</th><th>অবস্থা</th><th>মেয়াদ বাকি</th><th></th></tr></thead>
              <tbody>
                {holds.map((h) => {
                  const v = view(h);
                  const open = v.status === "HELD";
                  return (
                    <tr key={h.holdId} className={v.status === "EXPIRED" ? "dim" : ""}>
                      <td>{h.className}</td>
                      <td><span className={"badge " + v.status}>{STATUS_BN[v.status] ?? v.status}</span></td>
                      <td>{open ? fmt(v.left) : "—"}</td>
                      <td className="acts">
                        <button disabled={!open || busy !== ""} onClick={() => run("pay" + h.holdId, async () => { await api.confirmHold(h.holdId, savedId); }, "ভর্তি ফি জমা হয়েছে, ভর্তি নিশ্চিত হয়েছে।")}>ভর্তি ফি দিন</button>
                        <button className="sec" disabled={!open || busy !== ""} onClick={() => { if (window.confirm("আবেদন বাতিল করবেন?")) run("cancel" + h.holdId, async () => { await api.cancelHold(h.holdId, savedId); }, "আবেদন বাতিল করা হয়েছে।"); }}>বাতিল</button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}