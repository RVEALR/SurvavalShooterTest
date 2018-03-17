#define DEBUG_ANALYTICS_RECORD

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UTJ.FrameCapturer;
using Newtonsoft.Json;
using System.IO;

public class RVEALRManager : MonoBehaviour
{
    public MovieRecorder recorder;
    public Animator anim;

    public List<RvealrRecord> storedRecords = new List<RvealrRecord>();
    private List<RvealrRecord> archivedRecords = new List<RvealrRecord>();

    private float maxTimeRecordStoredCountdown = MAX_TIME_RECORDS_BATCH;

    private const int MAX_NUM_RECORDS_BATCH = 5;       //maximum records to accumulate before batch sending
    private const float MAX_TIME_RECORDS_BATCH = 30.0f; //maximum seconds to accumulate records before batch sending

    public static string _guidToken = System.Guid.NewGuid().ToString();

    private string remoteUri = "";

    public static RVEALRManager Instance = null;

    // Use this for initialization
    void Start ()
    {
        Instance = this;
        if (recorder == null)
        {
            recorder = GetComponent<MovieRecorder>();
        }

    }

    public void SendRecord(RvealrRecord analyticsRecord)
    {
         analyticsRecord.record_data["guid_token"] = _guidToken;
         DebugAnalyticsRecord(analyticsRecord);
         StoreRecord(analyticsRecord);
    }

    public void StoreRecord(RvealrRecord analyticsRecord)
    {
        storedRecords.Add(analyticsRecord);

        if (storedRecords.Count >= MAX_NUM_RECORDS_BATCH)
        {
            SendBatchedRecords();
        }
    }

    public void SendBatchedRecords()
    {
        string serializedData = JsonConvert.SerializeObject(storedRecords);
        WWWForm form = new WWWForm();
        form.AddField("data", serializedData);

        if (!string.IsNullOrEmpty(remoteUri))
        {
            UnityWebRequest www = UnityWebRequest.Post(remoteUri, form);
            www.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            SendAnalyticsNetworkRequest(www);
        }
        else
        {
            string outPath = recorder.outputDir.GetFullPath() + "/" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";

            using (FileStream fs = new FileStream(outPath, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    writer.Write(serializedData);
                    writer.Close();
                    writer.Dispose();
                }
                fs.Close();
                fs.Dispose();
            }
            Debug.Log(serializedData);
        }

        //archive records for later
        this.archivedRecords = new List<RvealrRecord>(storedRecords);
        this.storedRecords = new List<RvealrRecord>();
        maxTimeRecordStoredCountdown = MAX_TIME_RECORDS_BATCH;
    }

    private IEnumerator SendAnalyticsNetworkRequest(UnityWebRequest www)
    {
        yield return www.Send();

        while (!www.isDone)
        {
            yield return null;
        }

        if (www.isNetworkError)
        {
            this.storedRecords.AddRange(archivedRecords);
            this.archivedRecords.Clear();
        }
        else
        {
            this.archivedRecords.Clear();
        }
    }


    private void DebugAnalyticsRecord(RvealrRecord analyticsRecord)
    {
#if DEBUG_ANALYTICS_RECORD
			string debugString = "Sending analytics event : " + analyticsRecord.record_id + " : ";

			foreach(KeyValuePair<string,object> kvp in analyticsRecord.record_data)
			{
				debugString+="  " + kvp.Key + " : " + kvp.Value.ToString();
			}

			Debug.Log(debugString);
#endif
    }

    // Update is called once per frame
    void Update ()
    {
	}

    public void RecordGameEvent(float time = 4f)
    {
        if (!recorder.isRecording)
        {
            recorder.startTime = Time.unscaledTime;
            recorder.isRecording = true;
            recorder.captureControl = RecorderBase.CaptureControl.TimeRange;
            recorder.endTime = time;

            if (anim != null)
            {
                anim.enabled = true;
            }
        }
    }

    public class RvealrRecord
    {
        public string record_id;
        public Dictionary<string, object> record_data;

        protected void Init(string recordId, Dictionary<string, object> recordData = null)
        {
            this.record_data = recordData ?? new Dictionary<string, object>();
            this.record_data["device_time"] = DateTimeUtil.GetDeviceTime();
            this.record_data["device_time_ms"] = DateTimeUtil.GetMillisecondsSinceEpoch().ToString();

            record_id = recordId;
        }

        public RvealrRecord(Dictionary<string, object> recordData = null)
        {
            Init("", recordData);
        }
    }

    public static class DateTimeUtil
    {
        public static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime FromSecondsSinceEpoch(long timestamp)
        {
            return epoch.AddSeconds(timestamp);
        }

        public static long ToSecondsSinceEpoch(DateTime date)
        {
            return (long)(date.ToUniversalTime() - epoch).TotalSeconds;
        }

        public static long GetSecondsSinceEpoch()
        {
            return (long)(DateTime.UtcNow - epoch).TotalSeconds;
        }

        public static DateTime FromMillisecondsSinceEpoch(long timestamp)
        {
            return epoch.AddMilliseconds(timestamp);
        }

        public static long ToMillisecondsSinceEpoch(DateTime date)
        {
            return (long)(date.ToUniversalTime() - epoch).TotalMilliseconds;
        }

        public static long GetMillisecondsSinceEpoch()
        {
            return (long)(DateTime.UtcNow - epoch).TotalMilliseconds;
        }

        public static string GetDeviceTime()
        {
            return DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss zzz");
        }
    }
}
