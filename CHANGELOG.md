# Changelog

## 2.0.2 - 2026-10-12

- Documentato il ruolo centrale di `le64.exe` nell'emissione e nel rinnovo dei certificati.
- Precisati i parametri gestiti dall'applicazione, la posizione dei log e il fatto che `le64.exe` è un prerequisito esterno.

## 2.0.1 - 2026-10-11

- Sostituita la verifica OpenSSL `-checkhost`, assente nelle vecchie versioni XAMPP, con lettura diretta dei nomi SAN tramite .NET.
- Migliorato il messaggio di errore indicando i nomi effettivamente presenti nel certificato.
- Mantenuta la compatibilità Windows 7 x64 SP1.

## 2.0.0 - 2026-10-11

- Aggiunta gestione di più domini nello stesso certificato SAN.
- Aggiunti selettori per cartella sito, cartella certificati, OpenSSL, le64 e file VirtualHost.
- Aggiunto rilevamento degli Apache attivi con associazione al servizio Windows quando disponibile.
- Aggiunto profilo XML persistente per il rinnovo periodico con percorsi personalizzati.
- Aggiunta build x64 e compatibilità dichiarata con Windows 7 SP1 e .NET Framework 4.8.
- Mantenuto il flusso protetto HTTP, challenge porta 80, HTTPS, backup e rollback.

## 1.1.0 - 2026-10-05

- Aggiunto supporto alla document root principale di `htdocs`.
- Aggiunte indicazioni visibili nell'interfaccia e versioning dell'eseguibile.

## 1.0.0 - 2026-10-04

- Prima versione dell'applicazione grafica e del rinnovo pianificato.
