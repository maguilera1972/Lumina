# Lumina

Logiciel Windows 11 de **création / lecture de shows laser ILDA**, destiné à un PC dédié. Ce n’est pas un clone de Mamba Black : c’est un programme original qui **importe** les shows MediaLas, les **cale sur le MP3**, et permet de **prévisualiser avant projection**.

## Atelier (sans USB-Box)

Double-cliquez `Lumina.exe` : **aucune Box, aucun PowerShell, aucun SDK**. L’onglet **Atelier** permet de :

1. **Ouvrir** un show (`.ild`, `.fra`, Mamba, projet `*.lumina.json`) ou **Nouveau show**
2. **Importer** des motifs ILDA dans la bibliothèque, les **placer** sur la timeline
3. **Éditer** le clip (intensité, couleur, rotation, taille, décalage)
4. **Prévisualiser** à l’écran (lecture / pause / stop, forme d’onde si MP3)
5. **Enregistrer** le projet (Ctrl+S)

La USB-Box est dans un panneau replié « facultatif ». Le projecteur reste éteint tant que vous n’armez pas **LASER ON**.

## Ce qui fonctionne déjà

- **Prévisualisation avant projection** : le projecteur reste éteint (`LASER OFF`) ; l’écran montre le laser en même temps que l’audio
- Forme d’onde MP3, tête de lecture commune, clic pour caler
- Timeline éditable (scènes, durée, plage de frames) enregistrée dans `*.lumina.json`
- Onglet **Contenu ILDA** : liste des frames, points, format, aperçu image par image
- Onglet **Palette couleurs** : projecteur **RGB** ou **RGY**, conversion ILDA, étalonnage par diode, motifs test
- Import Mamba (`.xrs` / `.xmb` / `.mrs` / `.shw`) + `.ild` + `.fra`
- Association MP3 plus tard (le CD Mamba n’embarque souvent pas la musique)
- Arrêt d’urgence : **Échap**
- Paquet autonome pour un autre PC Windows 11

La sortie USB-Box MediaLas n’est pas encore branchée. `LASER ON` prépare le mode projecteur ; aujourd’hui la prévisualisation écran est la référence de synchro.

## Palette RGB / RGY (USB-Box)

Les projecteurs Lumina sont **RGB** (rouge, vert, bleu) ou **RGY** (rouge, vert, jaune — pas de bleu). L’onglet **Palette couleurs** verrouille les diodes et le câblage analogique du driver :

| Projecteur | Broche ILDA 5 | Broche ILDA 6 | Broche ILDA 7 (USB-Box) |
|---|---|---|---|
| RGB | rouge | vert | bleu |
| RGY | rouge | vert | **jaune** (la voie « bleu » ILDA pilote la diode jaune) |

La USB-Box MediaLas n’a que **trois** sorties analogiques couleur (FTDI D2XX, même schéma que l’ILDA). Un RGY n’a donc pas de 4ᵉ pin : le logiciel envoie le jaune sur la voie bleu. L’aperçu écran montre le jaune perçu ; l’export ILDA et le DAC envoient les niveaux analogiques.

Étalonnage (courbes 0 / 25 / 50 / 75 / 100 % par diode) : motifs test Rouge / Vert / Bleu ou Jaune / Blanc. Enregistrer le show écrit le profil dans `*.lumina.json`.

### Espace de projection

Dans **Palette couleurs**, section **ESPACE DE PROJECTION** : largeur, hauteur, alignement X/Y, rotation, miroir. **Mire d’alignement** affiche une grille (aperçu écran, et mur si **LASER ON**). **Enregistrer le profil…** / **Charger un profil…** utilise un fichier `*.lumina-profile.json` (couleurs + géométrie), réutilisable d’un show à l’autre. Les mêmes réglages sont aussi dans le sidecar du show.

## Vérifier le synchro audio / laser

1. Ouvrir le show (ou un `.ild`)
2. **Charger l’audio** (MP3) — ou poser `NomDuShow.mp3` à côté du fichier
3. Rester en **LASER OFF — prévisualisation**
4. Lecture : la forme d’onde, la timeline et l’aperçu laser avancent ensemble
5. Cliquer la forme d’onde ou la timeline pour se caler sur un passage
6. Ajuster les scènes (début, durée, frames), puis **Enregistrer la timeline**

Si les durées audio et laser divergent de plus de 0,5 s, un message l’indique à droite de l’aperçu.

## Audio sans fichier pour l’instant

Les MP3 peuvent arriver plus tard :

1. Même dossier, même nom : `Tonight.xrs` + `Tonight.mp3`
2. Un seul MP3 dans le dossier
3. Bouton **Charger l’audio…** → sidecar `Tonight.lumina.json`

## PC dédié Windows 11

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish-win11.ps1
```

Copier `dist\Lumina\` sur la machine de show et lancer `Lumina.exe`. Copier shows + MP3 + fichiers `.lumina.json`.

## Développement

Prérequis : [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet test Lumina.sln
dotnet run --project src\Lumina.App\Lumina.App.csproj
```

Ne commitez pas les MP3 ni les binaires MediaLas.
