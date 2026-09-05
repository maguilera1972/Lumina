# Layout iPad pour Lumina Live (OSC)

## OSC/PILOT (.plt) — important

Le format `.plt` d’OSC/PILOT est du JSON **propriétaire** (propriétés = IDs numériques
non documentés). Générer un `.plt` valide « from scratch » sans modèle produit
généralement un fichier **refusé** à l’ouverture.

### Pour obtenir `Lumina-Live.plt`

1. Sur l’iPad, exportez / partagez votre projet OSC/PILOT actuel (même le layout DJ).
2. Copiez le fichier ici sous le nom :
   `extras/osc-pilot/input.plt`
3. Sur le PC, lancez :
   ```powershell
   cd extras\osc-pilot
   powershell -ExecutionPolicy Bypass -File .\patch-plt-for-lumina.ps1
   ```
4. Uploadez `Lumina-Live.plt` dans OSC/PILOT iPad.
5. Vérifiez socket **A** :
   - **Host** = IP affichée dans Lumina (Live → OSC)
   - **Send To Port** = `9000`
6. Lumina : Live → OSC UDP → **Démarrer** (port 9000). Coupez le VPN sur l’iPad.

Les pads doivent envoyer `/lumina/pad/1` … `/lumina/pad/N` (On=`1`, Off=`0`)
et la coupure `/lumina/panic`.

---

## Alternative immédiate (sans .plt)

### A) Page web Lumina (déjà dans l’app)
Live → serveur web → Safari sur l’iPad → pads + aperçu ILDA.

### B) Open Stage Control
Fichier : `Lumina-Live.openstage.json`  
Cible OSC : IP du PC, port **9000**.

---

## Adresses Lumina

| Adresse | Valeur | Effet |
|---------|--------|--------|
| `/lumina/pad/N` | float/int `1` | press pad N (1-based) |
| `/lumina/pad/N` | `0` | release |
| `/lumina/panic` | `1` | coupure |
