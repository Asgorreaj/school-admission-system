export type ClassItem = { id: string; name: string; totalSeats: number; heldSeats: number; confirmedSeats: number; availableSeats: number }; // class
export type HoldItem = { holdId: string; classId: string; className: string; studentId: string; status: string; expiresAt: string; serverTime: string }; // hold

const BASE = "/api"; // nginx (ba vite proxy) backend-e pathay

function bn(status: number, msg?: string): string { // error-ke Bengali message-e
  const m = (msg ?? "").toLowerCase(); // lowercase
  if (m.includes("no seats")) return "দুঃখিত, এই ক্লাসে আর কোনো আসন খালি নেই।"; // seat shesh
  if (m.includes("already admitted")) return "এই শিক্ষার্থী ইতিমধ্যে এই ক্লাসে ভর্তি হয়েছে।"; // age bhorti
  if (m.includes("active application")) return "এই ক্লাসে এই শিক্ষার্থীর একটি চলমান আবেদন আছে।"; // duplicate
  if (m.includes("expired")) return "আবেদনের মেয়াদ শেষ হয়ে গেছে।"; // expired
  if (m.includes("cancelled")) return "এই আবেদন বাতিল করা হয়েছে।"; // cancelled
  if (m.includes("confirmed admission")) return "নিশ্চিত ভর্তি বাতিল করা যায় না।"; // confirmed cancel
  if (m.includes("another student")) return "এটি অন্য শিক্ষার্থীর আবেদন।"; // onner hold
  if (m.includes("17-digit") || m.includes("x-student-id")) return "জন্ম নিবন্ধন নম্বর ঠিক ১৭ অঙ্কের হতে হবে।"; // vul id
  if (m.includes("name is required")) return "ক্লাসের নাম দিন (সর্বোচ্চ ১০০ অক্ষর)।"; // nam
  if (m.includes("totalseats")) return "আসন সংখ্যা ১ থেকে ১,০০,০০০ এর মধ্যে হতে হবে।"; // seat
  if (status === 404) return "খুঁজে পাওয়া যায়নি।"; // 404
  if (status >= 500) return "সার্ভারে সমস্যা হয়েছে। একটু পরে আবার চেষ্টা করুন।"; // 5xx
  return "ভুল তথ্য দেওয়া হয়েছে। অনুগ্রহ করে আবার দেখুন।"; // default 4xx
}

async function call<T>(path: string, o: { method?: string; body?: unknown; sid?: string } = {}): Promise<T> { // sob request ekhan diye
  const headers: Record<string, string> = {}; // header
  if (o.body) headers["Content-Type"] = "application/json"; // json body
  if (o.sid) headers["X-Student-Id"] = o.sid; // student id
  let res: Response; // response
  try { res = await fetch(BASE + path, { method: o.method ?? "GET", headers, body: o.body ? JSON.stringify(o.body) : undefined }); } // request
  catch { throw new Error("সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। ইন্টারনেট বা সার্ভার ঠিক আছে কিনা দেখুন।"); } // network error
  if (res.status === 204) return undefined as T; // body nai
  const text = await res.text(); // text
  let data: any = null; // parsed json
  try { data = text ? JSON.parse(text) : null; } catch { data = null; } // parse
  if (!res.ok) throw new Error(bn(res.status, data?.error)); // error
  return data as T; // success
}

export const listClasses = () => call<ClassItem[]>("/classes"); // GET /classes
export const createClass = (name: string, totalSeats: number) => call<ClassItem>("/classes", { method: "POST", body: { name, totalSeats } }); // POST /classes
export const createHold = (classId: string, sid: string) => call<HoldItem>(`/classes/${classId}/holds`, { method: "POST", sid }); // abedon
export const myHolds = (sid: string) => call<HoldItem[]>("/me/holds", { sid }); // amar abedon
export const confirmHold = (id: string, sid: string) => call<HoldItem>(`/holds/${id}/confirm`, { method: "POST", sid }); // fee
export const cancelHold = (id: string, sid: string) => call<void>(`/holds/${id}`, { method: "DELETE", sid }); // batil