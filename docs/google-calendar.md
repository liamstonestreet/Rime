# Google Calendar and Tasks in Control Center

Menu shows every week of the current month in Sunday–Saturday rows (four to six rows as needed), with dimmed neighboring-month dates completing the first and last rows. A thin outline marks today, a soft fill marks the selected date, and dots mark dates with loaded events/tasks. Previous/next arrows browse by month; Today returns to the current day.

The **Timeline** below the calendar shows all-day events, dated incomplete tasks, and timed events for the selected day. It includes events overlapping that day, not only events that start on it. Dates and times follow the PC's local time zone; all-day end dates are exclusive. Clicking a row opens its Google event/task page when Google provides one. Editing and marking tasks complete are left to Google's UI.

## Private iCal setup (no Google Cloud)

Previous/next month navigation slides and fades over 220 ms. Weekday headings remain fixed; calendar height follows the month's row count. Repeated clicks replace the active transition; hidden views clear it. Windows' client-area animation preference disables this effect. CC and Spotlight use native mouse-wheel scrolling with no smoothing or wheel animation timer. The shared slim scrollbar and fixed gutter remain.

This is the simplest option for read-only calendar events. It needs no API key, OAuth project, or Google Cloud account. You can combine up to 20 calendars in one Timeline, including all five of your daily calendars.

1. Open [Google Calendar](https://calendar.google.com/) in your desktop browser.
2. Open **Settings**, select a calendar under **Settings for my calendars**, then open **Integrate calendar**.
3. Copy **Secret address in iCal format**. Keep this link private: it grants read access to the calendar. Do not make your calendar public or paste the link into chat.
4. In **CC > Menu**, click **Private iCal**. Give the calendar a name and paste its link into the masked field underneath.
5. Click **+ Add calendar** for each additional calendar. Each gets a color marker and a source label in the Timeline. Click **Save calendars** to apply the whole list.
6. Use **Refresh** for a fresh download. While Menu is visible, CC checks at most every five minutes. Google may cache a feed, so updates are not guaranteed to appear instantly.

Open **Private iCal** again to rename, replace, or remove individual connections. **Cancel** leaves the saved list unchanged. Removing all rows and saving removes only the iCal connections; **Disconnect** clears both the iCal links and any OAuth token. Reinstalling the pack preserves connections.

Links are encrypted with Windows DPAPI in `%LOCALAPPDATA%\NordRice\GoogleCalendar\feeds.bin`. They never go into the pack, repository, logs, or Zebar bridge. Fields remain masked when you reopen the dialog. A failed encryption operation does not fall back to plaintext. Network requests use HTTPS directly to Google Calendar; redirects are rejected. Event data stays in memory.

This option includes **events, not Google Tasks**, and cannot edit events. Each calendar requires its own link. Some managed accounts or calendars you do not own may not offer a secret address; ask the owner/admin about the available sharing options. Google describes this method in its [calendar synchronization instructions](https://support.google.com/calendar/answer/37648?hl=en).

The bounded iCal reader handles all-day/multi-day events, floating/UTC/VTIMEZONE times, daily/weekly/monthly/yearly rules, intervals, COUNT/UNTIL, BYDAY/BYMONTHDAY/BYMONTH/BYSETPOS, EXDATE/RDATE, and individual moved/cancelled occurrences. Unrecognized timezones and unsupported rules (such as hourly repeats, RDATE periods, BYWEEKNO/BYYEARDAY, or THISANDFUTURE overrides) produce an omitted-series notice, rather than silently inventing dates. Large feeds are bounded to 8 MB with cancellable parsing. A failed calendar does not prevent the others loading; a notice identifies unavailable calendars. A total failure keeps the previously displayed snapshot with an error until a successful refresh or a connection change.

Use either feeds or OAuth for a given calendar. Connecting the same calendar through both methods can show duplicate events.

## Optional Google sign-in setup (Calendar + Tasks)

This desktop helper needs its own OAuth application. Connecting Google Calendar to ChatGPT does not authorize a separately running Windows application.

1. Open [Google Cloud Console](https://console.cloud.google.com/) and create or select a project, such as **Nord Rice Calendar**.
2. Under **APIs & Services → Library**, enable **Google Calendar API** and **Google Tasks API** in that project.
3. Open **Google Auth Platform**. Complete **Branding** with an app name and your contact email. Under **Audience**, choose the appropriate audience for your account. For a personal Gmail account, use **External**, keep it in **Testing** initially, and add your own Google email as a test user.
4. Under **Data Access**, add these read-only scopes:
   - `https://www.googleapis.com/auth/calendar.readonly`
   - `https://www.googleapis.com/auth/tasks.readonly`
5. Under **Clients**, create an **OAuth client ID** with application type **Desktop app**, then download its JSON file. A Web application client or a service-account key is not suitable. You do not need to enter a callback port; the desktop app uses a temporary local loopback port.
6. Reinstall/restart the rice, open **CC → Menu**, and click **Google sign-in** below Timeline. Select the downloaded Desktop app JSON, then finish sign-in and consent in your normal browser. Only authorize a project you created and recognize.
7. Return to CC. Events from calendars selected/visible in Google Calendar and incomplete dated tasks from Google Tasks will load. **Refresh** fetches updates immediately; **Reconnect** starts Google sign-in again.

The **Setup steps** control in CC expands a short version of these instructions and a Google Cloud link.

Google's [desktop OAuth documentation](https://developers.google.com/identity/protocols/oauth2/native-app) describes the browser/loopback/PKCE flow used here. Google [limits refresh tokens for External apps in Testing to seven days](https://developers.google.com/identity/protocols/oauth2#expiration), so that setup may require weekly reconnection. Moving to Production is a separate Google project setting subject to Google's audience/verification requirements; an older Testing token should be replaced by reconnecting afterward. Workspace accounts may also be governed by their administrator's OAuth policy.

## What appears

- **Calendar:** Primary and other non-hidden calendars currently selected in Google Calendar. Recurring instances are expanded by Google's API for the displayed full-month grid range; cancelled events are excluded. Google calendar colors are used as small timeline/dot accents.
- **Tasks:** Incomplete, non-deleted/non-hidden tasks with a due/scheduled date in that range. Undated tasks cannot be assigned to a selected day, so they are omitted. Google's [Tasks API exposes only the date, not the scheduled time](https://developers.google.com/workspace/tasks/reference/rest/v1/tasks), so these rows are labelled **Task** rather than inventing a time.
- **Partial failures:** A notice identifies unavailable calendars or Tasks access. A total Calendar failure is shown as an error; previously loaded data can remain visible with the error notice. It is never replaced with demo events.

## Privacy and synchronization

Client metadata and Google tokens are stored under `%LOCALAPPDATA%\NordRice\GoogleCalendar`, encrypted with Windows DPAPI for the current Windows user. They are independent of the widget pack, so reinstalling the theme preserves sign-in. Nothing is stored in source files or sent through the Zebar bridge. If Windows encryption is unavailable, the app fails rather than saving plaintext credentials. Keep the original downloaded OAuth JSON out of the repository.

Event/task data is held in memory only. Selecting another date in the loaded range does not issue another API request. The visible Menu refreshes at most every five minutes; hidden tabs do not poll Google. Pagination is followed for calendars, events, task lists and tasks, and network calls are asynchronous/cancellable. Disconnect removes this PC's saved token and clears its in-memory calendar data; it does not revoke the grant in your Google Account. To revoke the grant globally, remove the app under your Google Account's connected apps.

## Validation

The isolated control-center checks use synthetic calendar data and make no Google requests. They cover four/five/six-week month grids, month/year navigation, short-month selection clamping, day transitions, chronological grouping, all-day exclusivity, midnight and DST boundaries, Tasks date semantics, PKCE/state validation and the encrypted vault when a Windows user profile is available. This sandbox cannot perform a DPAPI round-trip because it lacks a loaded user profile; that check verifies failure leaves no plaintext and reports a skip. Live sign-in/API synchronization requires the desktop client and your consent, so it cannot be validated before setup.
