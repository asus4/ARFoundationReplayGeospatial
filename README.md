# AR Foundation Replay with Google Geospatial Example

[AR Foundation Replay](https://github.com/asus4/ARFoundationReplay) is a proof of concept for recording AR Foundation session into a single mp4 with a binary timeline track / Replaying it in the Unity Editor on AR Foundation without any extra settings.

<https://github.com/asus4/ARFoundationReplay/assets/357497/8e77ee45-6f2c-442e-a47a-f35f044b8181>

↓ Check out the [complete project](https://github.com/asus4/WorldEnsemble) utilizing ARFoundationReplay.

<https://github.com/asus4/WorldEnsemble/assets/357497/1ff03fd4-01cf-41a6-8aef-42e11f7a67a2>

## Setup

This project loads AR Foundation Replay and the forked ARCore Extensions from local folders, so clone all three repositories side by side. Use the `replay` branch of the fork.

```sh
git clone https://github.com/asus4/ARFoundationReplay.git
git clone -b replay https://github.com/asus4/arcore-unity-extensions.git
git clone https://github.com/asus4/ARFoundationReplayGeospatial.git
```

Then open `ARFoundationReplayGeospatial` with Unity 6.6.
