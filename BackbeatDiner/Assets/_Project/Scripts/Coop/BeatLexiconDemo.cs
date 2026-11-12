using UnityEngine;
using UnityEngine.InputSystem;

namespace BackbeatDiner.Coop
{
    /// <summary>
    /// Placeholder for KG-3 beat lexicon: keys 1–4 play short procedural SE motifs.
    /// </summary>
    public sealed class BeatLexiconDemo : MonoBehaviour
    {
        [SerializeField] private AudioSource source;

        private AudioClip[] _motifs;

        private void Awake()
        {
            if (source == null)
            {
                source = gameObject.GetComponent<AudioSource>();
                if (source == null)
                {
                    source = gameObject.AddComponent<AudioSource>();
                }
            }

            source.playOnAwake = false;
            source.spatialBlend = 0f;
            _motifs = new[]
            {
                BuildMotif("Lexicon_C3", new[] { 261.63f, 329.63f }, 0.12f),
                BuildMotif("Lexicon_Need", new[] { 392.00f, 349.23f, 329.63f }, 0.09f),
                BuildMotif("Lexicon_Ready", new[] { 523.25f, 659.25f }, 0.10f),
                BuildMotif("Lexicon_Warn", new[] { 220.00f, 196.00f, 174.61f }, 0.11f)
            };
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            {
                Play(0, "C3 / 料理");
            }
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
            {
                Play(1, "不足");
            }
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
            {
                Play(2, "準備OK");
            }
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
            {
                Play(3, "注意");
            }
        }

        private void Play(int index, string label)
        {
            if (_motifs == null || index < 0 || index >= _motifs.Length)
            {
                return;
            }

            source.PlayOneShot(_motifs[index], 0.85f);
            Debug.Log($"[BeatLexiconDemo] Send motif {index + 1}: {label}");
        }

        private static AudioClip BuildMotif(string name, float[] freqs, float noteSeconds)
        {
            const int sampleRate = 44100;
            var gap = 0.02f;
            var total = noteSeconds * freqs.Length + gap * (freqs.Length - 1);
            var samples = Mathf.CeilToInt(total * sampleRate);
            var data = new float[samples];

            var cursor = 0;
            for (var n = 0; n < freqs.Length; n++)
            {
                var len = Mathf.CeilToInt(noteSeconds * sampleRate);
                for (var i = 0; i < len && cursor + i < samples; i++)
                {
                    var t = i / (float)sampleRate;
                    var env = Mathf.Exp(-t * 14f) * (1f - t / noteSeconds);
                    data[cursor + i] = Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * env * 0.55f;
                }

                cursor += len + Mathf.CeilToInt(gap * sampleRate);
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
