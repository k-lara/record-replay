# Additive Record and Replay

This Record and Replay system was implemented for the final paper of my PhD, published at IEEEVR 2026: **"Record Replay Repeat: Improving Interactivity Between Non-Player Characters with Additive Record and Replay"**.

**Abstract**: Non-player characters (NPCs) are a vital part of many 3D and virtual reality (VR) experiences, but animating them can be a time-consuming and costly process, often requiring expensive motion-capture setups, especially when tracking multiple people simultaneously. Single-user record and replay in VR enables a single user to animate multiple NPCs on their own, offering a cheaper and more convenient solution as the devices are consumer-grade and already track user movements. A limitation of single-user record and replay is that an early recorded character has no information about a later recorded character and might not be able to interact with them believably. We therefore study interactivity between NPCs as an emergent property when actors can see other characters in later recordings as opposed to acting primarily on their own in the first recording. We conducted a user study (N=144) where participants compared first, second, and third recording runs of different social scenarios with multiple NPCs. Our results suggest that for scenarios with low and medium interactivity where timing is not crucial and characters are not in close contact, a single run can be sufficient, whereas for high-interactivity scenarios, with close-contact group interactions, two recordings are more appropriate.

### Additional Info
This was a PhD project and not a software development project and therefore stuff needed to work fast, so things might be messy, or lack documentation. Sorry about that :)

The code for the additive record and replay tool is in **branch *additive_recordings***. (The *master* branch has some older record and replay approach. The *recordings-post-processing branch* has some code for creating videos from the raw recordings.)

The **APKs** used by the actors in the study and to record base recordings can be found in **[Releases](http://https://github.com/k-lara/record-replay/releases "Releases")**.
