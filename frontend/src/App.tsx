
import { useCallback, useEffect, useRef, useState } from "react"; // React hooks
import * as api from "./api"; // API functions
import type { ClassItem, HoldItem } from "./api"; // API types

const SID_RE = /^\d{17}$/; // 17-digit student ID pattern
const STATUS_BN: Record<string, string> = { HELD: "Active (HELD)", CONFIRMED: "Admission Confirmed (CONFIRMED)", EXPIRED: "Expired (EXPIRED)", CANCELLED: "Cancelled (CANCELLED)" }; // Status labels
const ts = (s: string) => Date.parse(s.replace(/(\.\d{3})\d+/, "$1")); // Truncate fractional seconds to 3 digits
const fmt = (sec: number) => `${String(Math.floor(sec / 60)).padStart(2, "0")}:${String(sec % 60).padStart(2, "0")}`; // Format as mm:ss

export default function App() { // Main application component
  const [savedId, setSavedId] = useState(localStorage.getItem("studentId") ?? ""); // Restore the saved ID after refresh
  const [draft, setDraft] = useState(savedId); // Student ID input
  const [classes, setClasses] = useState<ClassItem[] | null>(null); // Class list
  const [holds, setHolds] = useState<HoldItem[] | null>(null); // Application list
  const [classErr, setClassErr] = useState(""); // Class loading error
  const [holdErr, setHoldErr] = useState(""); // Application loading error
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null); // Success or error message
  const [busy, setBusy] = useState(""); // Currently running action
  const [now, setNow] = useState(Date.now()); // Current time for countdown
  const [name, setName] = useState(""); // New class name
  const [seats, setSeats] = useState(""); // New class seat count
  const offset = useRef(0); // Difference between server time and browser time
  const idOk = SID_RE.test(savedId); // Check whether the student ID is valid

  const loadClasses = useCallback(async () => { // Load classes
    try { setClasses(await api.listClasses()); setClassErr(""); } // Handle successful response
    catch (e) { setClassErr((e as Error).message); } // Handle loading error
  }, []);

  const loadHolds = useCallback(async () => { // Load applications
    if (!SID_RE.test(savedId)) { setHolds(null); return; } // Skip loading if the ID is invalid
    try {
      const h = await api.myHolds(savedId); // Fetch the student's applications
      if (h.length > 0) offset.current = ts(h[0].serverTime) - Date.now(); // Calculate server and browser clock difference
      setHolds(h); setHoldErr(""); // Update application data
    } catch (e) { setHoldErr((e as Error).message); } // Handle loading error
  }, [savedId]);

  useEffect(() => { loadClasses(); const t = setInterval(loadClasses, 3000); return () => clearInterval(t); }, [loadClasses]); // Refresh class data every 3 seconds
  useEffect(() => { loadHolds(); const t = setInterval(loadHolds, 3000); return () => clearInterval(t); }, [loadHolds]); // Refresh application data every 3 seconds
  useEffect(() => { const t = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(t); }, []); // Update the countdown every second

  const view = (h: HoldItem) => { // Calculate application status and remaining time
    const left = Math.max(0, Math.ceil((ts(h.expiresAt) - (now + offset.current)) / 1000)); // Calculate remaining seconds
    return { status: h.status === "HELD" && left <= 0 ? "EXPIRED" : h.status, left }; // Display EXPIRED when the countdown reaches zero
  };

  async function run(key: string, fn: () => Promise<void>, okText: string) { // Shared action handler
    setBusy(key); setMsg(null); // Start the action
    try { await fn(); setMsg({ ok: true, text: okText }); } // Handle successful action
    catch (e) { setMsg({ ok: false, text: (e as Error).message }); } // Handle action error
    finally { setBusy(""); await Promise.all([loadClasses(), loadHolds()]); } // Reset loading state and refresh data
  }

  function saveId(e: React.FormEvent) { // Save the student ID
    e.preventDefault(); // Prevent page reload
    const v = draft.trim(); // Remove surrounding whitespace
    if (!SID_RE.test(v)) { setMsg({ ok: false, text: "The birth registration number must contain exactly 17 digits." }); return; } // Validate the student ID
    localStorage.setItem("studentId", v); setSavedId(v); // Save the student ID
    setMsg({ ok: true, text: "Student information has been saved." }); // Display success message
  }

  function addClass(e: React.FormEvent) { // Create a class
    e.preventDefault(); // Prevent page reload
    const n = Number(seats); // Convert seat count to a number
    if (!name.trim()) { setMsg({ ok: false, text: "Please enter a class name." }); return; } // Validate class name
    if (!Number.isInteger(n) || n < 1) { setMsg({ ok: false, text: "The seat count must be a whole number greater than or equal to 1." }); return; } // Validate seat count
    run("class", async () => { await api.createClass(name.trim(), n); setName(""); setSeats(""); }, "Class created successfully."); // Submit class creation request
  }

  const active = new Set((holds ?? []).filter((h) => ["HELD", "CONFIRMED"].includes(view(h).status)).map((h) => h.classId)); // Classes with an active application

  return (
    <div className="wrap">
      <h1>School Admission System</h1>
      {msg && <div className={msg.ok ? "msg ok" : "msg err"} role="alert">{msg.text}</div>}

      {/* Student Information */}
      <section>
        <h2>Student Information</h2>
        <form onSubmit={saveId} className="row">
          <input value={draft} onChange={(e) => setDraft(e.target.value)} placeholder="17-digit birth registration number" inputMode="numeric" maxLength={17} aria-label="Birth registration number" />
          <button type="submit">Save</button>
        </form>
        <p className="hint">{idOk ? <>Current student: <b>{savedId}</b></> : "Enter your birth registration number before applying."}</p>
      </section>

      {/* Create Class (School Admin) */}
      <section>
        <h2>Create New Class (School Admin)</h2>
        <form onSubmit={addClass} className="row">
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Class 6 - Morning Shift" maxLength={100} aria-label="Class name" />
          <input value={seats} onChange={(e) => setSeats(e.target.value)} placeholder="Total seats" inputMode="numeric" aria-label="Total seats" className="small" />
          <button type="submit" disabled={busy !== ""}>{busy === "class" ? "Please wait..." : "Create"}</button>
        </form>
      </section>

      {/* Class List */}
      <section>
        <h2>Class List</h2>
        {classErr && <div className="msg err">{classErr}</div>}
        {!classes && !classErr && <p className="hint">Loading...</p>}
        {classes && classes.length === 0 && <p className="hint">No classes available yet. Create a new class above.</p>}
        {classes && classes.length > 0 && (
          <div className="scroll">
            <table>
              <thead><tr><th>Class</th><th>Total</th><th>Held</th><th>Confirmed</th><th>Available</th><th></th></tr></thead>
              <tbody>
                {classes.map((c) => (
                  <tr key={c.id}>
                    <td>{c.name}</td><td>{c.totalSeats}</td><td>{c.heldSeats}</td><td>{c.confirmedSeats}</td><td><b>{c.availableSeats}</b></td>
                    <td>
                      <button
                        disabled={!idOk || busy !== "" || active.has(c.id) || c.availableSeats <= 0}
                        onClick={() => run("apply" + c.id, async () => { await api.createHold(c.id, savedId); }, "Application submitted successfully. Complete the admission fee payment before the hold expires.")}
                      >
                        {active.has(c.id) ? "Already Applied" : c.availableSeats <= 0 ? "No Seats Available" : busy === "apply" + c.id ? "Please wait..." : "Apply"}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* My Applications */}
      <section>
        <h2>My Applications</h2>
        {!idOk && <p className="hint">Enter your birth registration number above to view your applications.</p>}
        {idOk && holdErr && <div className="msg err">{holdErr}</div>}
        {idOk && !holds && !holdErr && <p className="hint">Loading...</p>}
        {idOk && holds && holds.length === 0 && <p className="hint">This student has no applications yet.</p>}
        {idOk && holds && holds.length > 0 && (
          <div className="scroll">
            <table>
              <thead><tr><th>Class</th><th>Status</th><th>Time Remaining</th><th></th></tr></thead>
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
                        <button disabled={!open || busy !== ""} onClick={() => run("pay" + h.holdId, async () => { await api.confirmHold(h.holdId, savedId); }, "Admission fee submitted. Admission confirmed.")}>Pay Admission Fee</button>
                        <button className="sec" disabled={!open || busy !== ""} onClick={() => { if (window.confirm("Are you sure you want to cancel this application?")) run("cancel" + h.holdId, async () => { await api.cancelHold(h.holdId, savedId); }, "Application cancelled."); }}>Cancel</button>
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
