# Réveil musical

Service qui réveille un utilisateur avec un morceau choisi selon le **jour de la semaine** et la **météo**, puis le notifie sur son canal préféré. Une panne du fournisseur musical, du canal ou du service de préférences ne l'empêche jamais d'envoyer : mode dégradé, jamais de silence.

Point d'entrée : `IWakeUpService.SendWakeUpAsync(WakeUpRequest)` — `userId`, jour, météo (`Soleil`, `Pluie`, `Neige`, `Nuageux`). Aucun appel météo.

Architecture, patterns et choix de durées de vie : [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Lancer

Prérequis : SDK .NET 10.

```
dotnet build
dotnet test
dotnet run --project src/ReveilMusical.Host -- <userId> <jour> <météo>
```

Exemple : `dotnet run --project src/ReveilMusical.Host -- alice vendredi pluie`

- `<jour>` : `lundi` … `dimanche` ; `<météo>` : `SOLEIL`, `PLUIE`, `NEIGE`, `NUAGEUX` (casse indifférente).
- Arguments absents : `alice lundi soleil`. Valeur invalide : message d'usage, code de sortie 2.
- Utilisateurs de démonstration : `alice` (email, toutes les météos), `bob` (SMS, ni neige ni nuages : son morceau de secours s'applique), `chloe` (push). Tout autre identifiant reçoit les préférences par défaut.
- La console affiche le morceau, le canal utilisé et l'indicateur de mode dégradé. Code de sortie 1 si aucun canal n'a pu notifier.

La démonstration appelle les vraies API iTunes et MusicBrainz. Avant usage réel, remplacer `CONTACT_A_REMPLACER` dans `Music:MusicBrainz:UserAgent` (`src/ReveilMusical.Host/appsettings.json`) par un contact : MusicBrainz exige un User-Agent identifiable.

## Changer de fournisseur (config / variable d'environnement)

L'ordre des fournisseurs est celui de `Music:Providers`. Aucune recompilation.

Dans `appsettings.json` :

```json
"Music": { "Providers": ["musicbrainz", "itunes"] }
```

Par variable d'environnement (prioritaire sur le fichier) :

```
Music__Providers__0=musicbrainz
Music__Providers__1=itunes
```

Ou en ligne de commande : `dotnet run --project src/ReveilMusical.Host -- alice lundi pluie --Music:Providers:0=musicbrainz --Music:Providers:1=itunes`

Noms valides : `itunes`, `musicbrainz`, `local`. Un nom inconnu arrête l'application au démarrage avec un message listant les noms valides. Une liste vide n'utilise que le catalogue local.

Autres réglages : `Music:HttpTimeoutSeconds`, `Music:CacheTtlMinutes`, `Notification:DefaultChannel`, `Notification:FallbackOrder`, `Defaults:FallbackTrack`.

Ajouter un fournisseur : une classe `internal` qui implémente `IMusicCatalog`, et son enregistrement nommé dans `ServiceCollectionExtensions`. La suite `MusicCatalogContractTests` se réutilise par une sous-classe qui fournit les corps JSON.

## Ajouter un canal (WhatsApp) en 3 étapes

Aucun fichier existant du domaine ou de l'application n'est modifié : l'identifiant de canal est une chaîne (`ChannelId`), pas un enum.

1. Écrire l'interface du client et son simulateur dans `src/ReveilMusical.Infrastructure/Notifications`, par exemple `IWhatsAppApi` et `LogWhatsAppApi`.
2. Écrire l'adaptateur `WhatsAppChannel : INotificationChannel` avec `Id = new ChannelId("whatsapp")` ; il lève si le client échoue.
3. Ajouter les lignes d'enregistrement dans `ServiceCollectionExtensions.AddInfrastructure` :

```csharp
services.AddTransient<IWhatsAppApi, LogWhatsAppApi>();
services.AddTransient<INotificationChannel, WhatsAppChannel>();
```

Le dispatcher reçoit tous les `INotificationChannel` enregistrés. Tant que `whatsapp` n'est pas enregistré, un utilisateur qui le préfère est notifié par les canaux de `Notification:FallbackOrder`. Le test `NouveauCanalEnregistre_EstUtiliseSansModifierLExistant` le démontre.

## Modes dégradés (matrice)

| Panne | Comportement | `Degraded` |
|---|---|---|
| Service de préférences en panne ou utilisateur inconnu | préférences par défaut (`Defaults:FallbackTrack`, `Notification:DefaultChannel`), réveil envoyé | non |
| Météo non couverte par l'utilisateur | morceau de secours de l'utilisateur | non |
| Un fournisseur lève, dépasse le délai, répond 429/500 ou ne trouve rien | fournisseur suivant | non |
| Tous les fournisseurs en panne | morceau du catalogue local | oui |
| Canal préféré en panne ou inconnu | canaux de `FallbackOrder`, un par un | oui |
| Tous les canaux en panne | log `Critical`, `ChannelUsed = null`, aucune exception | oui |
| Annulation (`CancellationToken`) | propagée, jamais avalée | — |

Deux replis distincts :

- **Repli fonctionnel** : l'utilisateur n'a pas choisi de morceau pour cette météo, on prend *son* morceau de secours. C'est le comportement normal.
- **Repli technique** : aucun fournisseur ne répond, on prend un morceau de la liste locale en dur. C'est un mode dégradé, signalé par `Degraded = true`.

## Dépendances (licence et fraîcheur)

Méthode : avant chaque ajout, licence lue dans le `.nuspec` du package et dernière version stable relevée sur nuget.org ; tout copyleft est refusé. Audit final le 2026-10-08 avec `dotnet list package --include-transitive`, `--outdated --include-transitive` et `--vulnerable --include-transitive` : **aucun package vulnérable, aucun copyleft**, 41 packages sur 42 à la dernière version stable.

Plateforme : SDK .NET 10.0.401, runtime 10.0.12 (licence MIT, version LTS supportée jusqu'au 2028-11-14) — dernières versions stables publiées.

### Directes

| Package | Projet | Version installée | Licence | Dernière stable | Statut |
|---|---|---|---|---|---|
| Microsoft.Extensions.Logging.Abstractions | Application | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.DependencyInjection.Abstractions | Application | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Http | Infrastructure | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Configuration.Binder | Infrastructure | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Hosting | Host | 10.0.12 | MIT | 10.0.12 | à jour |
| xunit | Tests | 2.9.3 | Apache-2.0 | 2.9.3 | à jour (voir justification) |
| xunit.runner.visualstudio | Tests | 4.0.0 | Apache-2.0 | 4.0.0 | à jour |
| Microsoft.NET.Test.Sdk | Tests | 18.10.1 | MIT | 18.10.1 | à jour |
| coverlet.collector | Tests | 10.1.0 | MIT | 10.1.0 | à jour |

Le domaine n'a aucune dépendance.

### Transitives

| Package | Version installée | Licence | Dernière stable | Statut |
|---|---|---|---|---|
| Microsoft.Extensions.Configuration | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Configuration.CommandLine | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Configuration.FileExtensions | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Configuration.Json | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Configuration.UserSecrets | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.DependencyInjection | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Diagnostics | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Diagnostics.Abstractions | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.FileProviders.Abstractions | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.FileProviders.Physical | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.FileSystemGlobbing | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Logging | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Logging.Configuration | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Logging.Console | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Logging.Debug | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Logging.EventLog | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Logging.EventSource | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Options | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.Extensions.Primitives | 10.0.12 | MIT | 10.0.12 | à jour |
| System.Diagnostics.EventLog | 10.0.12 | MIT | 10.0.12 | à jour |
| Microsoft.CodeCoverage | 18.10.1 | MIT | 18.10.1 | à jour |
| Microsoft.TestPlatform.ObjectModel | 18.10.1 | MIT | 18.10.1 | à jour |
| Microsoft.TestPlatform.TestHost | 18.10.1 | MIT | 18.10.1 | à jour |
| xunit.core | 2.9.3 | Apache-2.0 | 2.9.3 | à jour |
| xunit.assert | 2.9.3 | Apache-2.0 | 2.9.3 | à jour |
| xunit.extensibility.core | 2.9.3 | Apache-2.0 | 2.9.3 | à jour |
| xunit.extensibility.execution | 2.9.3 | Apache-2.0 | 2.9.3 | à jour |
| xunit.abstractions | 2.0.3 | Apache-2.0 (par URL) | 2.0.3 | à jour (voir justification) |
| xunit.analyzers | 1.18.0 | Apache-2.0 | 2.1.0 | en retard (voir justification) |

### Composants qui posent question

- **xunit.analyzers 1.18.0 (dernière stable : 2.1.0)** — transitive imposée par `xunit` 2.9.3, lui-même à sa dernière version. C'est un analyseur de compilation utilisé uniquement par le projet de tests : il n'est ni livré ni exécuté en production. Risque accepté ; il se résorbera avec la migration ci-dessous.
- **xunit 2.9.3** — dernière version du package `xunit`, celle du gabarit `dotnet new xunit`. La génération suivante est publiée sous un autre identifiant (`xunit.v3`) : la migration est une montée de version majeure à planifier, pas une mise à jour de routine.
- **xunit.abstractions 2.0.3** — package ancien dont le `.nuspec` déclare sa licence par URL et non par expression SPDX. Le texte pointé a été lu : Apache License 2.0, permissive.
- **Montées majeures faites à l'initialisation** — le gabarit installait `coverlet.collector` 6.0.4, `Microsoft.NET.Test.Sdk` 17.14.1 et `xunit.runner.visualstudio` 3.1.4. Ils ont été portés à 10.1.0, 18.10.1 et 4.0.0, puis validés par l'exécution des tests et de la collecte de couverture.
- **Services externes (hors packages)** — iTunes Search (Apple) et MusicBrainz sont des API tierces gratuites et sans clé, soumises aux conditions d'utilisation de leurs éditeurs. Contraintes connues de l'énoncé : environ 20 requêtes par minute pour iTunes (d'où le cache), User-Agent identifiable obligatoire pour MusicBrainz. Ces conditions sont à faire valider avant une mise en production. Les deux sont isolées derrière `IMusicCatalog` : en changer ne touche qu'un adaptateur.

Bibliothèques volontairement écartées : bibliothèques de mock, Polly, Scrutor, FluentAssertions, AutoMapper. Les simulateurs et la résilience sont écrits à la main.

## Tests et couverture

198 tests, aucun appel réseau (HTTP simulé par `StubHttpMessageHandler`, simulateurs écrits à la main).

- Tests unitaires par composant.
- Tests de contrat réutilisés : `MusicCatalogContractTests` (iTunes et MusicBrainz) et `NotificationChannelContractTests` (email, SMS, push).
- Test de composition : le vrai conteneur résout `IWakeUpService` ; l'ordre de `Music:Providers` change l'ordre des appels.
- Scénarios de bout en bout « pas de silence ».
- Tests d'architecture par réflexion : sens des dépendances, DTO fournisseurs `internal`, aucun nom de fournisseur ou de canal dans le domaine et l'application, aucun champ statique mutable.

```
dotnet test --collect:"XPlat Code Coverage"
```

Taux lus dans `coverage.cobertura.xml` (attribut `line-rate`), mesurés le 2026-10-08 :

| Périmètre | Lignes couvertes |
|---|---|
| Domain | 100 % |
| Application | 100 % |
| Infrastructure | 100 % |
| **Hors Host** | **100 %** (475 / 475) |
| Host | 82,35 % (`Program.cs`, la console de démonstration, n'est pas testé) |
| Global | 96,88 % |

## Hypothèses

- **Usage du jour de la semaine** : l'énoncé fournit le jour mais les préférences ne dépendent que de la météo. Le jour sert donc à composer le message ; le morceau est choisi par la météo.
- **Annuaire de contacts hors périmètre** : le destinataire (adresse, numéro, jeton d'appareil) est dérivé de `userId`.
- **Pertinence** : le premier résultat du fournisseur est retenu ; il peut s'agir d'une reprise.
- **Préférences par défaut** : un réveil envoyé avec les préférences par défaut n'est pas marqué `Degraded` ; l'indicateur couvre le repli local et le changement de canal.
- **`null` non caché** : seuls les succès sont mis en cache. Un morceau introuvable ou une panne est redemandé au prochain réveil.
- **Cache par fournisseur** : le cache décore chaque fournisseur réseau, pas la chaîne ; un repli local n'est donc jamais mis en cache pendant une panne.
- **Pas de circuit breaker** : chaque réveil réessaie les fournisseurs dans l'ordre, avec un délai maximal par appel (`Music:HttpTimeoutSeconds`). Piste d'évolution : circuit breaker, et cache distribué derrière `ICache`.
- **Ordonnancement** : le déclenchement à l'heure du réveil n'est pas codé.
