# 011 — Player identity for the playtest

**Owner:** backend-engineer
**Size:** M
**Status:** done
**GDD:** docs/gdd/GDD.md#technical-strategy (Game Center / Google Play Games sign-in)
**Depends on:** 010

## Goal
Each install gets a player account on the server without any typing, so the server can
tell players apart and every later request is authenticated.

## Acceptance criteria
- [x] Server: `POST /auth/anonymous` takes an install id (a GUID the client generates once
      and keeps) and creates or returns that install's player id, stored in a Postgres
      `players` table, and issues a signed token (signing key from env). Every other
      endpoint except `/health` rejects requests without a valid token (401).
- [x] Client: `Assets/Scripts/Network/` (namespace `DoomScroller.Network`, in the main
      assembly) gets a `ServerClient` with the base URL from a ScriptableObject or
      settings asset (not hardcoded), install-id creation and storage on the device, a
      sign-in call, token storage on the device, and an
      authenticated request helper with a timeout. Calls are async and testable behind
      an interface (`IServerClient`) so EditMode tests can fake it.
- [x] Server tests: new install gets a new id; same install id returns the same player;
      bad or missing token gets 401.
- [x] EditMode tests: `ServerClient` attaches the stored token; a fake transport
      returning 401 triggers one re-sign-in then fails cleanly.
- [x] (manual, Miles) With the dev server running, entering Play mode signs in and the
      server logs one player id that stays the same across Play sessions.

## Context
- Memory: memory/systems.md (Save and persistence)
- May read: `Server/`, `Inventory/ProfileController.cs` (startup order only), GDD above
- May write: `Server/`, `Assets/Scripts/Network/` (new), new test files
  `Assets/Tests/EditMode/ServerClientTests.cs`

## Out of scope
- Game Center / Google Play Games linking (a later task before soft launch), account
  deletion, per-device boss limits.

## Notes
- Decision (Miles, Q3): anonymous sign-in by install id now; Game Center / Google Play
  Games linking comes before soft launch, not in this slice. Keep the `players` table
  ready for a linked platform id later (nullable column or separate table), but build
  no linking.
- `DoomScroller.Network` folder/namespace is approved by Miles.
- Unity web requests (`UnityWebRequest`) need the main thread; keep the transport behind
  the interface so the logic stays testable.
- Manual check FAILED (Miles, 2026-11-12): "With the dev server running, entering Play mode signs in and the…" — Didnt see sign in in server logs this was most recent output server-1  | info: Microsoft.AspNetCore.Hosting.Diagnostics[1] server-1  |       Request starting HTTP/1.1 GET http://localhost:8088/health - - - server-1  | info: Microsoft.AspNetCore.Routing.EndpointMiddleware[0] server-1  |       Executing endpoint 'HTTP: GET /health' server-1  | info: Microsoft.AspNetCore.Http.Result.OkObjectResult[1] server-1  |       Setting HTTP status code 200. server-1  | info: Microsoft.AspNetCore.Http.Result.OkObjectResult[3] server-1  |       Writing value of type '<>f__AnonymousType0`3' as Json. server-1  | info: Microsoft.AspNetCore.Routing.EndpointMiddleware[1] server-1  |       Executed endpoint 'HTTP: GET /health' server-1  | info: Microsoft.AspNetCore.Hosting.Diagnostics[2] server-1  |       Request finished HTTP/1.1 GET http://localhost:8088/health - 200 - application/json;+charset=utf-8 36.4609ms
- Manual check FAILED (Miles, 2026-11-12): "With the dev server running, entering Play mode signs in and the…" — [Server] Sign-in failed (is the dev server running?) UnityEngine.Debug:LogWarning (object) DoomScroller.Network.ServerSession/<Start>d__4:MoveNext () (at Assets/Scripts/Network/ServerSession.cs:19) System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<bool>:SetResult (bool) DoomScroller.Network.ServerClient/<SignInAsync>d__11:MoveNext () (at Assets/Scripts/Network/ServerClient.cs:41) System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<DoomScroller.Network.ServerResponse>:SetResult (DoomScroller.Network.ServerResponse) DoomScroller.Network.UnityWebRequestTransport/<SendAsync>d__0:MoveNext () (at Assets/Scripts/Network/UnityWebRequestTransport.cs:33) System.Threading.Tasks.TaskCompletionSource`1<bool>:TrySetResult (bool) DoomScroller.Network.UnityWebRequestTransport/<>c__DisplayClass0_0:<SendAsync>b__0 (UnityEngine.AsyncOperation) (at Assets/Scripts/Network/UnityWebRequestTransport.cs:27) UnityEngine.AsyncOperation:InvokeCompletionEvent () (at /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/AsyncOperation.cs:24)
- Cause of failed manual check (backend-engineer): the dev container was a 2-day-old image without /auth/anonymous (404). Rebuilt with `docker compose up --build`; sign-in now returns a player id and /auth/me without a token returns 401. Re-check in Play mode. If Unity still warns, Player Settings > Allow downloads over HTTP is "Not allowed".
- Approved for merge (Miles, 2026-11-12).
