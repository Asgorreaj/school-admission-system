# Assumptions

- **Student ID**: must be exactly 17 digits (as the brief says); otherwise 400. There is no student table, the ID is just an identity key.
- **No login / roles**: as per the brief, anyone can use the class-creation form.
- **IDs** are opaque strings with a prefix (`c_...`, `h_...`).
- **Class names are not unique**; the ID identifies a class. `totalSeats` must be 1 to 100000.
- **Active application** = a HELD hold that has not expired, or a CONFIRMED hold. After cancel or expiry the student may apply again for the same class.
- **Expiry**: a HELD hold whose `expiresAt` passed is reported as EXPIRED immediately (computed at read time) and ignored when counting seats, so the seat is released without any API call. A background worker also persists the EXPIRED status every 5 seconds.
- **Status codes**: 403 for confirming/cancelling another student's hold, 409 for no seats / duplicate / expired / cancelled, 404 for unknown IDs.
- **Confirm** on an already CONFIRMED hold returns 200 with the same result (idempotent). **DELETE** on an already CANCELLED hold returns 204. Cancelling a CONFIRMED or EXPIRED hold returns 409.
- `GET /holds/{holdId}` needs no header (as in the brief's example); hold IDs are random and unguessable.
- **Payment** is simulated: pressing "ভর্তি ফি দিন" calls confirm.
- **Time**: the backend UTC clock decides expiry. The UI shows a countdown using the server time returned by the API, so a wrong browser clock does not matter.
- **Schema** is created with `EnsureCreated` at startup (no migrations) to keep `docker compose up` simple.
- **Secrets**: docker-compose has a dev-only default DB password; override it with a `.env` file for any real use.

## How concurrency is handled

- Creating a hold runs in a PostgreSQL transaction that first locks the class row (`SELECT ... FOR UPDATE`), then counts active HELD + CONFIRMED holds, then inserts. Requests for the same class are serialized by the database, so held + confirmed can never exceed `totalSeats`.
- Because the lock lives in PostgreSQL (not in memory), it also works when **two backend processes** share one database (bonus requirement).
- Confirm and cancel lock the hold row (`FOR UPDATE`), so repeated or parallel confirms count once.
- A partial unique index on `(StudentId, ClassId)` for HELD/CONFIRMED holds guarantees one active application per student per class, even if application code had a bug.
- A check constraint keeps `totalSeats >= 1`.
- Known limit: multiple backend processes rely on roughly synchronized clocks for expiry.