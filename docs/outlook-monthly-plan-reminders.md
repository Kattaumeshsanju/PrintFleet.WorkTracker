# Outlook monthly plan reminders

WorkTracker sends weekday monthly-plan reminders from one authorized Outlook mailbox through Microsoft Graph. The existing 5:00 PM daily reminder and Team Notices keep their current behavior. When Graph is configured, those messages also leave from the same mailbox. Until Graph is ready, the daily reminder and Team Notices continue to use the existing SMTP settings, and plan reminders are not sent.

Automated plan reminders stay off until an admin turns them on under Email Reminders.

## Microsoft 365 setup

Use an Entra application with a certificate or a managed identity. Do not put the mailbox password, certificate password, or client secret in source control. Put the certificate password only in `appsettings.*.local.json` or the host secret store. That file is gitignored.

1. Register an Entra application for WorkTracker. Upload a certificate and record the tenant id, client id, and certificate thumbprint.
2. Create the Exchange Online service principal for that application.
3. Create a management scope limited to the one sender mailbox, for example `sanju.k@intechadditive.com`.
4. Assign the Exchange role `Application Mail.Send` to that service principal on that scope only.
5. Do not add an unscoped Entra `Mail.Send` application permission. A tenant-wide Graph `Mail.Send` grant lets the app send as any mailbox and cancels the Exchange scope.
6. Set `Email:FromAddress` to that same mailbox. The app sends only as this address and saves the message in Sent Items when Graph accepts the request.
7. Set `Email:Graph:TenantId`, `Email:Graph:ClientId`, and either `CertificateThumbprint`, `CertificatePath`, or `UseManagedIdentity`.

Graph `202 Accepted` means Microsoft queued the message. WorkTracker records that as `Accepted`, not delivered.

## Delegated OAuth alternative

If application permission cannot be approved, an admin can sign in with delegated `Mail.Send` and the app can store a refresh token in a secret store. That path is not implemented. A refresh token stops working when the user revokes consent, changes password, or when conditional access blocks the sign-in, so a weekday job can fail while nobody is present. Application authentication with a mailbox-scoped Exchange role is the path this build uses.

## Configuration

```json
"Email": {
  "FromAddress": "sender@company.com",
  "FromName": "WorkTracker",
  "SiteUrl": "http://192.168.3.135:5174",
  "Graph": {
    "TenantId": "",
    "ClientId": "",
    "CertificateThumbprint": "",
    "UseManagedIdentity": false
  }
}
```

The plan reminder clock is Asia/Kolkata. The default time is 17:00, Monday to Friday. Admins can change the plan-reminder days and time. The separate daily reminder still uses `Email:Hour` and `Email:Minute`.

## What is sent

Each active team lead and team member gets one personal message for the current month. A person with no saved monthly sheet gets a missing-plan note when that option is on. Team leads can also receive one department summary. Summaries contain counts, deadlines, and goal titles the lead can already see in WorkTracker. They do not contain daily timeline text. Cancelled goals are listed apart from the counted plan. Admin and CEO accounts are not recipients unless they also have an active department member role, which this application does not assign together.

The same person and message type are not sent twice for the same date. A transient Graph failure can be retried up to three times. A permanent failure is not retried. A send that was already accepted is never retried.

No production test was sent from this implementation. Use Email Reminders → Send test to me after the certificate is installed.
