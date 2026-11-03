# Routes de donjons via Keystone.guru

Étude de l'API Keystone.guru (3 novembre 2026) avant d'implémenter des routes de donjons dans FrontPulse.
Rien n'est codé pour l'instant. Seules l'entité `DungeonRoute` et la table `dungeon_routes` existent déjà.

## Sources

- Documentation (Swagger UI rendu en JavaScript) : https://keystone.guru/api/documentation
- Spécification OpenAPI lue par cette page : https://keystone.guru/docs?api-docs.json
- API « Keystone.guru API », version 1, base `https://keystone.guru/api/v1`

## Authentification

- **HTTP Basic sur toute l'API** : email et mot de passe d'un compte Keystone.guru.
- Un appel anonyme renvoie **401** (vérifié sur `GET /api/v1/dungeon`).
- Il faut donc un compte, de préférence dédié au site. Ses identifiants iraient en configuration :
  - en local : secrets utilisateur ;
  - en prod : `/etc/frontpulse/frontpulse.env` (`KeystoneGuru__Email`, `KeystoneGuru__Password`).

## Endpoints utiles

| Méthode | Chemin | Utilité |
|---|---|---|
| GET | `/api/v1/dungeon` | Liste des donjons |
| GET | `/api/v1/dungeon/{dungeon}` | Détail d'un donjon |
| GET | `/api/v1/route/{gameVersion}/{dungeon}/popular` | Routes populaires d'un donjon |
| GET | `/api/v1/route/{gameVersion}/{dungeon}/new` | Routes récentes d'un donjon |
| GET | `/api/v1/route/{gameVersion}/popular` | Routes populaires, tous donjons confondus |
| GET | `/api/v1/route` | Liste de routes, filtrable par `dungeon_id` |
| GET | `/api/v1/route/{route}` | Détail d'une route, pull par pull |

**Paramètres des routes populaires et récentes :**
- `gameVersion` : `retail` ;
- `dungeon` : slug du donjon, par exemple `ara-kara-city-of-echoes` ;
- `offset` : 0 par défaut ;
- `count` : 10 par défaut, 100 au maximum.

L'API expose aussi des endpoints de miniatures (`POST /route/{route}/thumbnail`, `GET /route/thumbnailJob/{id}`). On n'en a pas besoin.

## Données disponibles

**Résumé d'une route (`DungeonRouteSummary`)**
- `dungeonId`, `publicKey`, `title`, `expiresAt` ;
- `enemyForces` / `enemyForcesRequired`, pour le pourcentage de mobs ;
- `pullCount` ;
- `author` : `name`, `avatar`, liens ;
- `affixGroups`, chaque affixe avec son `id` et son `name`.

**Liens d'une route (`links`)**
- `view` : la page de la route sur Keystone.guru ;
- `edit` ;
- `embed` : **la carte interactive, intégrable en iframe** ;
- `thumbnails` : les miniatures.

**Détail d'une route (`DungeonRoute`)** : le résumé, plus `pulls`, chaque pull avec sa `description`, ses `spells` et ses `enemies`.

**Donjon (`Dungeon`)**
- `id`, `expansion`, `name`, `slug`, `key` ;
- `zoneId`, `mapId`, `instanceId` ;
- `challengeModeId` : le même identifiant que côté Blizzard. Il permet de recouper avec les donjons de la saison lus sur Raider.io.
- `floors`.

## Limites

1. **Pas de chaîne d'export MDT dans l'API**, donc pas de bouton « Copier pour MDT ». Le mieux possible : un lien « Ouvrir sur Keystone.guru », où l'export MDT existe.
2. **Aucune information sur les limites d'appels ni sur les conditions d'utilisation** dans la spécification.
3. **Aucun champ de note ni de nombre de vues** dans le résumé : la popularité n'apparaît qu'à travers l'ordre de l'endpoint `popular`.

## À vérifier avant d'implémenter

- [ ] Les conditions d'utilisation de l'API, et l'accord de Keystone.guru pour afficher leurs routes et intégrer leurs cartes (iframe `embed`).
- [ ] Les limites d'appels, s'il y en a.
- [ ] La création d'un compte dédié à FrontPulse.
- [ ] Les slugs Keystone.guru par rapport aux slugs Raider.io des donjons de la saison (le recoupement se ferait par `challengeModeId`).

## Implémentation proposée

- **Worker** : à chaque ingestion, récupérer les routes populaires de chaque donjon de la saison (environ 8 appels, `count` = 10) et les stocker dans `dungeon_routes`.
- **Site** :
  - une page « Donjons » avec une carte par donjon ;
  - une page par donjon avec les 5 à 10 routes les plus populaires (titre, auteur, % de mobs, pulls, affixes), la carte `embed` de la route choisie et un lien « Ouvrir sur Keystone.guru (export MDT) » ;
  - une entrée « Donjons » dans la topbar.
- **Attribution** : créditer Keystone.guru et l'auteur sur chaque route, et ajouter Keystone.guru aux sources du pied de page.
