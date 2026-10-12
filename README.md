# Win Apache Certificate Manager

Versione corrente: **2.0.2**

Applicazione Windows x64 con interfaccia grafica per creare e rinnovare certificati HTTPS su installazioni Apache configurabili.

## Compatibilità

- Windows 7 x64 SP1 con .NET Framework 4.8
- Windows 10 x64
- Windows 11 x64

L'applicazione non usa PowerShell. Il rilevamento dei processi e servizi Apache usa API Windows e WMI disponibili su Windows 7.

La verifica dei domini SAN viene eseguita direttamente tramite .NET, senza richiedere l'opzione OpenSSL `-checkhost`; sono quindi supportate anche le vecchie versioni OpenSSL incluse in XAMPP su Windows 7.

## Campi configurabili

- uno o più domini, uno per riga;
- cartella completa del sito;
- cartella di destinazione di certificato e chiave;
- processo Apache selezionato tra quelli `httpd.exe` attivi;
- `openssl.exe`;
- `le64.exe`;
- file `httpd-vhosts.conf`.

Il primo dominio diventa `ServerName`. Gli altri diventano `ServerAlias` e vengono richiesti come nomi SAN nello stesso certificato.

## Ruolo centrale di ssl-le64

`le64.exe` è il client ACME che comunica con Let's Encrypt ed è il componente centrale per l'emissione e il rinnovo dei certificati. L'applicazione non sostituisce `le64.exe`: ne coordina l'esecuzione insieme ad Apache e OpenSSL.

Durante la prima configurazione l'applicazione:

1. attiva il VirtualHost HTTP e riavvia Apache;
2. esegue il `le64.exe` selezionato, passando tutti i domini, la cartella `.well-known\acme-challenge` e i percorsi di account, CSR, chiave e certificato;
3. lascia che `le64.exe` crei il file della challenge, richieda a Let's Encrypt la verifica HTTP-01 sulla porta 80 e generi il certificato;
4. verifica con .NET e OpenSSL il certificato prodotto, lo installa nella cartella scelta e attiva HTTPS.

Anche `AggiornaCertificati.bat` richiama indirettamente lo stesso `le64.exe`, tramite il percorso salvato in `GestioneCertificati.profiles.xml`. La richiesta di rinnovo usa una soglia di 20 giorni.

`le64.exe` non è incluso nel repository: deve essere già presente sul server. Il percorso proposto dall'interfaccia è `C:\server\ssl-le64\le64.exe`, ma può essere cambiato. I file di lavoro ACME e i log `gestione_*.log` e `comandi_*.log` vengono salvati nella cartella di `le64.exe`; nel log dei comandi è visibile la riga completa realmente eseguita.

## Procedura automatica

1. Valida domini, cartelle, eseguibili e configurazione Apache corrente.
2. Crea il backup di VirtualHost ed eventuali certificati precedenti.
3. Aggiunge il VirtualHost HTTP sulla porta 80.
4. Verifica la configurazione e riavvia l'Apache selezionato.
5. Avvia `le64.exe`, che esegue la challenge ACME HTTP-01 dalla cartella `.well-known\acme-challenge` del sito e richiede il certificato a Let's Encrypt.
6. Verifica tutti i domini SAN, validità temporale e corrispondenza tra certificato e chiave.
7. Installa certificato e chiave nella cartella selezionata.
8. Aggiunge il VirtualHost HTTPS sulla porta 443.
9. Verifica e riavvia nuovamente l'Apache selezionato.
10. Salva il profilo necessario al rinnovo periodico.

Se una fase fallisce, ripristina `httpd-vhosts.conf`, certificato e chiave precedenti e tenta il riavvio con la configurazione ripristinata.

## Selezione Apache

L'elenco mostra PID, eventuale nome del servizio Windows ed eseguibile completo. Se il processo è associato a un servizio, test e riavvio usano quel nome con `httpd.exe -n`. Se WMI non riesce ad associare il servizio, viene usato l'eseguibile selezionato senza `-n`.

## Rinnovo periodico

Tenere nella stessa cartella:

- `GestioneCertificati.exe`
- `AggiornaCertificati.bat`
- `GestioneCertificati.profiles.xml`, creato automaticamente dopo la prima configurazione riuscita

Pianificare `AggiornaCertificati.bat` giornalmente con **Esegui con i privilegi più elevati**. Ogni profilo conserva domini, cartelle, Apache, OpenSSL, le64 e file VirtualHost scelti.

Il rinnovo usa una soglia di 20 giorni. Se è troppo presto, non sostituisce file e non riavvia Apache.

## Prerequisiti esterni

- Tutti i domini devono risolvere verso il server.
- La porta TCP 80 pubblica deve raggiungere l'Apache selezionato.
- La porta TCP 443 deve essere pubblicata per il servizio HTTPS.
- Il file VirtualHost deve essere incluso dalla configurazione dell'Apache selezionato.
- `mod_ssl` e `mod_log_config` devono essere abilitati.
- L'utente deve avere permessi amministrativi.

## Build

Il sorgente è in `src/CertificatiWeb`. Eseguire `build.bat` per generare `build/GestioneCertificati.exe`.

La build è x64, richiede amministratore tramite manifest ed è compilata sul runtime CLR v4 compatibile con .NET Framework 4.8.

I file già compilati sono disponibili nella cartella `release`.

L'eseguibile non è firmato digitalmente.

## Licenza

Il progetto è distribuito con licenza [MIT](LICENSE).
