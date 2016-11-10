# EmailSenderBridge

Console application to listen azure service bus queue for messages (which are emails) and sends them (emails) using MimeKit

# Configuration

* Change ServiceBus settings in appsettings.json:

  *  NamespaceUrl - Service Bus url, just change XXX to service bus name.
  *  PolicyName - Name of the Shared access policy to use.
  *  Key - private key of the selected policy.
  *  QueueName - name of the queue in service bus to listen messages from.

* Change Smtp settings in appsettings.json:
  *  Host - smtp server host name or ip address
  *  Port - smtp server Port
  *  Login/Password - credentials for smtp auth on selected server
  *  From - email address to be used as "from" field of the email messages
  *  DisplayName - display name of the email address to be used in "from" field of the email message
  *  LocalDomain - The local domain is used in the HELO or EHLO commands sent to the SMTP server. If left unset, the local IP address will be used instead.
