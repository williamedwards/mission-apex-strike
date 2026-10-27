using System;
using System.Collections.Generic;
using System.IO;
using GravBoots.Level;
using GravBoots.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravBoots.Debugging
{
    /// <summary>One thing that happened during a playtest.</summary>
    [Serializable]
    public class PlaytestEvent
    {
        /// <summary>Seconds since the run started.</summary>
        public float t;
        /// <summary>start, checkpoint, death, respawn, boots, damage, complete, end.</summary>
        public string type;
        public Vector3 position;
        public Vector3 up;
        public string detail;
    }

    /// <summary>One run of one chamber, as written to disk.</summary>
    [Serializable]
    public class PlaytestRecord
    {
        public string scene;
        public string startedUtc;
        public float duration;
        public int deaths;
        public bool completed;
        public List<PlaytestEvent> events = new List<PlaytestEvent>();
        /// <summary>The player's position every <see cref="PlaytestLog"/> path interval.</summary>
        public List<Vector3> path = new List<Vector3>();
    }

    /// <summary>
    /// Records a run for watching playtests back: deaths (where and why), checkpoints, respawns,
    /// boots switches, damage, completion, and a coarse path. Written as JSON to
    /// <see cref="Folder"/> whenever something important happens, so a quit or crash loses
    /// little. GravBoots → Playtests → Show in Scene View draws the results.
    /// </summary>
    public class PlaytestLog : MonoBehaviour
    {
        [SerializeField] LevelSession session;
        [SerializeField] float pathInterval = 0.5f;

        PlaytestRecord record;
        PlayerMotor player;
        MagneticBoots boots;
        Health health;
        string file;
        float startTime, nextSample;
        bool dirty;

        /// <summary>Logs/Playtests in the project when in the Editor; the player's data folder in a build.</summary>
        public static string Folder => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Playtests"))
            : Path.Combine(Application.persistentDataPath, "Playtests");

        public PlaytestRecord Record => record;

        void OnEnable()
        {
            if (session == null)
                return;
            session.CheckpointChanged += OnCheckpoint;
            session.PlayerDied += OnDied;
            session.Respawned += OnRespawned;
            session.Completed += OnCompleted;
        }

        void OnDisable()
        {
            if (session != null)
            {
                session.CheckpointChanged -= OnCheckpoint;
                session.PlayerDied -= OnDied;
                session.Respawned -= OnRespawned;
                session.Completed -= OnCompleted;
            }
            if (boots != null)
                boots.StateChanged -= OnBoots;
            if (health != null)
                health.Damaged -= OnDamaged;
            if (record != null)
            {
                Add("end", null);
                Write();
            }
        }

        void Start()
        {
            if (session == null || session.Player == null)
                return;
            player = session.Player;
            boots = player.GetComponent<MagneticBoots>();
            health = player.GetComponent<Health>();
            if (boots != null)
                boots.StateChanged += OnBoots;
            if (health != null)
                health.Damaged += OnDamaged;

            DateTime now = DateTime.UtcNow;
            string scene = SceneManager.GetActiveScene().name;
            record = new PlaytestRecord { scene = scene, startedUtc = now.ToString("o") };
            file = Path.Combine(Folder, $"{scene}_{now:yyyyMMdd_HHmmss}.json");
            startTime = Time.time;
            Add("start", session.Checkpoint != null ? session.Checkpoint.name : null);
            Write();
        }

        void Update()
        {
            if (record == null || player == null || Time.time < nextSample)
                return;
            nextSample = Time.time + pathInterval;
            if (session.State == SessionState.Playing)
                record.path.Add(player.Position);
            if (dirty && record.path.Count % 20 == 0)
                Write();
        }

        void OnCheckpoint(SpawnPoint spawn)
        {
            if (record == null)
                return;
            Add("checkpoint", spawn != null ? spawn.name : null);
            Write();
        }

        void OnDied(DeathCause cause)
        {
            Add("death", cause.ToString());
            Write();
        }

        void OnRespawned() => Add("respawn", null);

        void OnBoots(BootsState from, BootsState to) => Add("boots", to.ToString());

        void OnDamaged(float amount) => Add("damage", amount.ToString("0"));

        void OnCompleted()
        {
            if (record == null)
                return;
            record.completed = true;
            Add("complete", null);
            Write();
        }

        void Add(string type, string detail)
        {
            if (record == null)
                return;
            record.events.Add(new PlaytestEvent
            {
                t = Time.time - startTime,
                type = type,
                position = player != null ? player.Position : Vector3.zero,
                up = player != null ? player.BodyUp : Vector3.zero,
                detail = detail,
            });
            dirty = true;
        }

        void Write()
        {
            if (record == null || file == null)
                return;
            record.duration = Time.time - startTime;
            record.deaths = session != null ? session.Deaths : 0;
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(file, JsonUtility.ToJson(record, true));
                dirty = false;
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[Playtest] Couldn't write {file}: {e.Message}");
            }
        }
    }
}
