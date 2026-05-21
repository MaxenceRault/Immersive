<div align="center">
  <br />
    <a href="https://www.iim.fr/" target="_blank">
      <img src="https://www.inventivestudio.co.uk/wp-content/uploads/2024/05/banner_final.jpg" alt="Project Banner">
    </a>
  
  <br />

<img src="https://skills-icons.vercel.app/api/icons?i=unity,csharp" />

  <h3 align="center">IIMersive Challenge</h3>

   <div align="center">
         Une expérience VR immersive — IIM Digital School
    </div>
</div>

## 🤖 <a name="introduction">Introduction</a>

**IIMersive** est un projet **Unity 6** en **C#**, conçu pour le **casque Meta Quest** (OpenXR). Vous explorez une **cantina post-apocalyptique** inspirée de l'univers Fallout : déplacements en VR, interactions à la manette, inventaire d'objets à placer, portes, éclairage, et un mini-jeu photo qui vous téléporte sur la route lorsque le cadre est au vert.

La scène principale est **Cantina** ; le projet cible une expérience **AR/VR** avec interactions immersives et ambiance sonore intégrée.

## 📋 <a name="table">Table of Contents</a>

1. 🤖 [Introduction](#introduction)
2. 🎮 [Contrôles (manette)](#controls)
3. ⚙️ [Tech Stack](#tech-stack)
4. 🔋 [Features](#features)
5. 🤸 [Quick Start](#quick-start)
6. 📝 [Notes](#notes)
7. 🚀 [More](#more)

## 🎮 <a name="controls">Contrôles (manette)</a>

- `Y` : ouvrir l'inventaire.
- Sélection dans l'inventaire : viser avec la gâchette droite + utiliser la gâchette gauche.
- Fermer l'inventaire puis `gâchette droite` : placer l'objet sélectionné.
- `A` : sauter et ouvrir la porte.
- `Joystick` : se déplacer.
- `X` : reset de la scène.
- `Gâchette intérieure droite` : grab / attraper un objet.
- `Gâchette droite` sur le bouton à côté de la porte : éteindre la lumière.

## ⚙️ <a name="tech-stack">Tech Stack</a>

**[Unity 6](https://unity.com/)** (6000.4.2f1) est le moteur du projet. Il gère la scène 3D, la physique, l'audio et le pipeline de rendu pour une build **Android / Meta Quest**.

**[C#](https://learn.microsoft.com/dotnet/csharp/)** est le langage des scripts gameplay : inventaire, portes, respawn, station photo, interactions XR, etc.

**[Universal Render Pipeline (URP)](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest)** fournit le rendu temps réel et le post-traitement (ambiance type Fallout post-nucléaire).

**[XR Interaction Toolkit](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@latest)** gère les interactors, le grab, le déplacement du joueur XR et les affordances de la manette.

**[OpenXR](https://www.khronos.org/openxr/)** est la couche XR utilisée pour cibler le runtime **Meta Quest** depuis Unity.

**[Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest)** centralise les bindings manette (inventaire, porte, reset scène, grab, etc.).

## 🔋 <a name="features">Features</a>

👉 **Système d'inventaire** : ouverture avec `Y`, sélection au raycast (gâchette droite + gauche), placement d'objets dans le monde après fermeture de l'inventaire.

👉 **Interactions environnementales** : ouverture de porte (`A`), bouton pour couper la lumière (gâchette droite), objets saisissables à la main.

👉 **Respawn automatique** : si le joueur tombe dans le vide, il réapparaît au point de spawn configuré.

👉 **Locomotion VR** : déplacement au joystick et actions de base à la manette Quest.

👉 **Post-traitement visuel** : rendu URP avec ambiance type **Fallout post-nucléaire**.

👉 **Ambiance sonore** : pas du joueur, sons de porte, placement d'objets et ambiance de la cantina.

👉 **Station photo** : prise de vue en VR ; un cadre **vert** déclenche une téléportation sur la route avec gel temporaire des déplacements.

👉 **Reset de scène** : bouton `X` pour recharger la scène **Cantina**.

## 🤸 <a name="quick-start">Quick Start</a>

### Prérequis

- **Unity Hub** avec l'éditeur **Unity 6** (6000.4.2f1 ou compatible).
- **Meta Quest** (ou émulateur XR Device Simulator inclus dans le projet).
- Module **Android Build Support** (SDK / NDK) pour une build sur casque.

### Ouvrir le projet

```bash
git clone <url-du-repo>
```

Puis dans **Unity Hub** : **Add** → sélectionner le dossier du projet → ouvrir avec **Unity 6000.4.2f1**.

### Scène et play mode

1. Ouvrir `Assets/Scenes/Cantina.unity`.
2. Brancher le casque ou activer le **XR Device Simulator** (samples XR Interaction Toolkit).
3. Appuyer sur **Play** dans l'éditeur, ou **File → Build Settings** → plateforme **Android** → profil **Meta Quest** → **Build And Run**.

### Build Meta Quest (résumé)

1. **Edit → Project Settings → XR Plug-in Management** : activer **OpenXR** et le profil Android Meta.
2. **File → Build Settings** : plateforme **Android**, scène **Cantina** cochée.
3. Utiliser le profil de build **Meta Quest** si présent dans `Assets/Settings/Build Profiles/`.
4. Connecter le casque en mode développeur et lancer la build.

## 📝 <a name="notes">Notes</a>

- Ce projet est orienté **expérience VR** avec interactions immersives (pas une app mobile ou web).
- Les contrôles listés correspondent à une **manette Meta Quest** ; les bindings peuvent être ajustés dans l'**Input Actions** du projet.
- Scripts principaux : `Assets/Scripts/` (`InventoryManager`, `DoorController`, `PlayerRespawn`, `CameraStation`, `PhotoTeleport`, etc.).

## <a name="more">🚀 More </a>

**Si vous voulez construire des projets avec Unity**

<br />
    <a href="https://unity.com" target="_blank">
       <img src=https://www.inventivestudio.co.uk/wp-content/uploads/2024/07/banner.png" alt="Github Footer" />
    </a>
<br />
