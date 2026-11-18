using UnityEngine;

namespace Clarenza
{
    public sealed class Classroom : MonoBehaviour
    {
        public Material surfaceMaterial;
        public CircuitState circuit = new CircuitState();
        int page;
        int question;
        int correct;
        int selected = -1;
        bool submitted;
        bool paused;
        float elapsed;
        string feedback = "";
        Camera view;
        Light lampLight;
        Renderer bulb;
        Transform switchLever;
        Transform batteryObject, lampObject;
        float yaw;
        float pitch=8;
        GUIStyle title, body, small, button;
        Texture2D card, accent, hover;
        Vector2 panelScroll;
        readonly string[] questions = {
            "What makes the lamp light in this model?",
            "What happens when the switch opens?",
            "What supplies energy to the circuit?"
        };
        readonly string[][] answers = {
            new [] { "A battery alone", "A complete path through the battery and lamp", "An open switch" },
            new [] { "Current stops and the lamp turns off", "The battery gets larger", "The lamp stays on" },
            new [] { "The switch", "The wire", "The battery" }
        };
        readonly int[] keys = { 1, 0, 2 };

        void Start()
        {
            Application.runInBackground = true;
            BuildRoom();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--smoke-test") >= 0) StartCoroutine(SmokeTest());
        }

        System.Collections.IEnumerator SmokeTest()
        {
            yield return new WaitForSeconds(1);
            string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Evidence"));
            System.IO.Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, "welcome.png"));
            yield return new WaitForSeconds(1);
            page = 2; circuit.battery = circuit.lamp = circuit.closed = true; circuit.Observe();
            yield return new WaitForSeconds(1);
            if (!circuit.Powered || !lampLight.enabled) { Debug.LogError("CLARENZA_SMOKE_FAILED: lamp"); Application.Quit(1); yield break; }
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, "circuit.png"));
            yield return new WaitForSeconds(1);
            circuit.closed = false; circuit.Observe();
            if (!circuit.Complete) { Debug.LogError("CLARENZA_SMOKE_FAILED: progression"); Application.Quit(1); yield break; }
            yield return new WaitForSeconds(1);
            page = 3;
            yield return new WaitForSeconds(1);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, "quiz.png"));
            yield return new WaitForSeconds(1);
            page = 4; correct = 3;
            yield return new WaitForSeconds(1);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, "complete.png"));
            yield return new WaitForSeconds(1);
            Restart();
            if (page != 0 || circuit.Complete || correct != 0) { Debug.LogError("CLARENZA_SMOKE_FAILED: restart"); Application.Quit(1); yield break; }
            Debug.Log("CLARENZA_RUNTIME_SMOKE_PASSED"); Application.Quit(0);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) paused = !paused;
            if (paused) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return; }
            if (page > 0 && page < 4) elapsed += Time.deltaTime;
            bool look = Input.GetMouseButton(1);
            Cursor.lockState = look ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !look;
            if (look)
            {
                yaw += Input.GetAxis("Mouse X") * 2;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2, -50, 50);
                view.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }
            if (page > 0)
            {
                Vector3 movement = view.transform.forward * Input.GetAxisRaw("Vertical") + view.transform.right * Input.GetAxisRaw("Horizontal");
                movement.y = 0;
                Vector3 position = view.transform.position + movement.normalized * Time.deltaTime * 2.5f;
                position.x = Mathf.Clamp(position.x, -5, 5);
                position.z = Mathf.Clamp(position.z, -5, -0.8f);
                position.y = 2.6f;
                view.transform.position = position;
            }
            circuit.Observe();
            batteryObject.position = new Vector3(.2f, circuit.battery ? 1.2f : 1.65f, 1.2f);
            lampObject.position = new Vector3(3, circuit.lamp ? 1.35f : 1.7f, 1.2f);
            lampLight.enabled = circuit.Powered;
            bulb.material.color = circuit.Powered ? new Color(1, .85f, .25f) : new Color(.3f, .36f, .4f);
            switchLever.localRotation = Quaternion.Euler(0, 0, circuit.closed ? 0 : 35);
        }

        Texture2D Tint(Color c) { var t=new Texture2D(1,1);t.SetPixel(0,0,c);t.Apply();return t; }
        void Styles()
        {
            if (title != null) return;
            card=Tint(new Color(.035f,.065f,.085f,.97f));accent=Tint(new Color(.09f,.25f,.28f));hover=Tint(new Color(.12f,.39f,.4f));
            title = new GUIStyle(GUI.skin.label) { fontSize = 29, fontStyle = FontStyle.Bold, wordWrap = true };
            body = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = 17, wordWrap = true, padding = new RectOffset(16, 16, 12, 12), margin=new RectOffset(0,0,5,5) };
            button.normal.background=accent;button.hover.background=hover;button.active.background=hover;
            button.normal.textColor=Color.white;button.hover.textColor=Color.white;
            small.normal.textColor=new Color(.62f,.76f,.78f);body.normal.textColor=new Color(.88f,.92f,.92f);
        }

        bool Action(string label) { return GUILayout.Button(label, button, GUILayout.MinHeight(46)); }
        void OnGUI()
        {
            Styles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.DrawTexture(new Rect(18, 18, width - 36, 66), card);
            GUI.Label(new Rect(36, 29, 600, 40), "CLARENZA  /  Study lab", title);
            GUI.Label(new Rect(width - 580, 40, 370, 30), "EXPLORE  /  EXPERIMENT  /  UNDERSTAND", small);
            GUI.DrawTexture(new Rect(18, 98, 390, height - 168), card);
            GUI.DrawTexture(new Rect(18,98,4,height-168),accent);
            GUI.Label(new Rect(432,110,450,28), "01   /   ENERGY & CIRCUITS", small);
            GUI.Label(new Rect(432,143,550,48), circuit.Powered?"A connection comes to life.":"Small experiments. Real understanding.",body);
            if(GUI.Button(new Rect(width-195,30,160,42),"Learning lobby",button)) UnityEngine.SceneManagement.SceneManager.LoadScene("Auditorium");
            GUILayout.BeginArea(new Rect(40, 116, 344, height - 205));
            panelScroll=GUILayout.BeginScrollView(panelScroll);
            if (paused)
            {
                GUILayout.Label("Lesson paused", title);
                if (Action("Return to learning lobby")) UnityEngine.SceneManagement.SceneManager.LoadScene("Auditorium");
                GUILayout.Label("Your current activity is still here.", body);
                if (Action("Resume lesson")) paused = false;
                if (Action("Restart from welcome")) Restart();
                if (Action("Quit application")) Application.Quit();
            }
            else if (page == 0)
            {
                GUILayout.Label("Make the connection.", title);
                GUILayout.Space(18);
                GUILayout.Label("Explore how a complete electrical circuit powers a lamp.", body);
                GUILayout.Space(18);
                GUILayout.Label("01  Learn the three components\n\n02  Build and break the circuit\n\n03  Check your understanding", body);
                GUILayout.Space(22);
                GUILayout.Label("Your personal space to learn by doing.", small);
                if (PlayerPrefs.HasKey("Clarenza.Completed")) GUILayout.Label("Previous completion: " + PlayerPrefs.GetInt("Clarenza.Score") + "/3 correct", small);
                if (Action("Start exploring")) page = 1;
            }
            else if (page == 1)
            {
                GUILayout.Label("01 / The essentials", title);
                GUILayout.Space(15);
                GUILayout.Label("BATTERY\nSupplies electrical energy.\n\nLAMP\nConverts electrical energy into light and heat.\n\nSWITCH\nCloses or opens the path for current.", body);
                GUILayout.Space(15);
                GUILayout.Label("In this simplified series circuit, the lamp lights only when both components are connected and the switch is closed.", body);
                GUILayout.Space(14);
                if (Action("Try the circuit")) page = 2;
                if (Action("Back")) page = 0;
            }
            else if (page == 2)
            {
                GUILayout.Label("02 / Circuit lab", title);
                GUILayout.Label("Connect both components, then close the switch. Next, open it and observe the lamp.", body);
                GUILayout.Space(12);
                if (Action(circuit.battery ? "Disconnect battery" : "Connect battery")) circuit.battery = !circuit.battery;
                if (Action(circuit.lamp ? "Disconnect lamp" : "Connect lamp")) circuit.lamp = !circuit.lamp;
                if (Action(circuit.closed ? "Open switch" : "Close switch")) circuit.closed = !circuit.closed;
                circuit.Observe();
                GUILayout.Space(12);
                GUILayout.Label(circuit.Powered ? "LAMP ON — complete circuit" : "LAMP OFF — circuit incomplete", body);
                GUILayout.Label((circuit.observedLight ? "DONE" : "TODO") + "  Light the lamp\n" + (circuit.observedOpen ? "DONE" : "TODO") + "  Open the switch after lighting it", small);
                GUI.enabled = circuit.Complete;
                if (Action("Check my understanding")) page = 3;
                GUI.enabled = true;
                if (Action("Review the essentials")) page = 1;
            }
            else if (page == 3)
            {
                GUILayout.Label("03 / Knowledge check", title);
                GUILayout.Label("Question " + (question + 1) + " of 3", small);
                GUILayout.Space(15);
                GUILayout.Label(questions[question], body);
                GUILayout.Space(15);
                for (int i = 0; i < 3; i++)
                {
                    GUI.enabled = !submitted;
                    if (Action((selected == i ? "● " : "○ ") + answers[question][i])) selected = i;
                }
                GUI.enabled = true;
                GUILayout.Label(feedback, body);
                if (!submitted)
                {
                    GUI.enabled = selected >= 0;
                    if (Action("Submit answer"))
                    {
                        submitted = true;
                        if (selected == keys[question]) { correct++; feedback = "Correct. " + answers[question][keys[question]] + "."; }
                        else feedback = "The answer is: " + answers[question][keys[question]] + ".";
                    }
                    GUI.enabled = true;
                }
                else if (Action(question == 2 ? "See my results" : "Next question"))
                {
                    if (question == 2) { page = 4; PlayerPrefs.SetInt("Clarenza.Completed", 1); PlayerPrefs.SetInt("Clarenza.Score", correct); PlayerPrefs.Save(); }
                    else { question++; selected = -1; submitted = false; feedback = ""; }
                }
            }
            else
            {
                GUILayout.Label("Lesson complete", title);
                GUILayout.Space(18);
                GUILayout.Label(correct + " / 3 correct", title);
                GUILayout.Label("You built a complete circuit and observed how opening a switch stops current.", body);
                GUILayout.Space(15);
                GUILayout.Label("Time in lesson: " + Mathf.CeilToInt(elapsed / 60) + " min\nResult saved on this computer.", small);
                GUILayout.Space(15);
                if (Action("Repeat lesson")) Restart();
                if (Action("Explore the lab again")) { page = 2; question = correct = 0; selected = -1; submitted = false; feedback = ""; }
                if (Action("Quit application")) Application.Quit();
            }
            GUILayout.EndScrollView();GUILayout.EndArea();
            GUI.Label(new Rect(30, height - 49, width - 60, 38), "WASD / arrows: move    •    Hold right mouse: look    •    Left click: lesson controls    •    Esc: pause", small);
        }

        void Restart()
        {
            circuit.Reset(); page = question = correct = 0; selected = -1; submitted = paused = false; elapsed = 0; feedback = "";
            view.transform.SetPositionAndRotation(new Vector3(-.4f, 2.6f, -5.4f), Quaternion.Euler(8,0,0)); yaw = 0;pitch=8;
        }

        GameObject Shape(string name, PrimitiveType type, Vector3 position, Vector3 size, Color color)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.position = position; obj.transform.localScale = size;
            var mat = new Material(surfaceMaterial); mat.color = color; obj.GetComponent<Renderer>().material = mat;
            return obj;
        }
        void Label(string text, Vector3 position, float size)
        {
            var obj = new GameObject(text); obj.transform.position = position;
            var label = obj.AddComponent<TextMesh>(); label.text = text; label.fontSize = 48; label.characterSize = size; label.anchor = TextAnchor.MiddleCenter; label.color = Color.white;
        }
        void BuildRoom()
        {
            RenderSettings.ambientLight = new Color(.6f, .65f, .7f);
            var cameraObject = new GameObject("Learner Camera"); view = cameraObject.AddComponent<Camera>(); view.tag = "MainCamera";
            view.transform.position = new Vector3(-.4f, 2.6f, -5.4f); view.backgroundColor = new Color(.06f, .1f, .16f); view.clearFlags = CameraClearFlags.SolidColor;
            view.fieldOfView = 58;view.transform.rotation=Quaternion.Euler(8,0,0);
            var sun = new GameObject("Classroom Light").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = .9f; sun.transform.rotation = Quaternion.Euler(45, -25, 0);
            Shape("Floor", PrimitiveType.Cube, new Vector3(0,-.15f,0), new Vector3(12,.3f,14), new Color(.28f,.31f,.32f));
            Shape("Front wall", PrimitiveType.Cube, new Vector3(0,2.5f,5), new Vector3(12,5,.2f), new Color(.1f,.16f,.19f));
            Shape("Left wall", PrimitiveType.Cube, new Vector3(-6,2,0), new Vector3(.2f,4,10), new Color(.38f,.43f,.43f));
            Shape("Right wall", PrimitiveType.Cube, new Vector3(6,2,0), new Vector3(.2f,4,10), new Color(.38f,.43f,.43f));
            Shape("Lesson board", PrimitiveType.Cube, new Vector3(1.6f,2.7f,4.8f), new Vector3(6,2,.1f), new Color(.03f,.2f,.23f));
            Label("THE CONNECTION LAB", new Vector3(1.6f,3.15f,4.7f), .09f);
            Label("ENERGY   /   LIGHT   /   DISCOVERY", new Vector3(1.6f,2.55f,4.7f), .045f);
            Shape("Lab table", PrimitiveType.Cube, new Vector3(1.6f,.85f,1.4f), new Vector3(4.5f,.15f,2), new Color(.65f,.49f,.32f));
            // Architectural detail: warm slats, acoustic panels, luminous windows and a supported workbench.
            for(int i=0;i<25;i++) Shape("Oak wall slat",PrimitiveType.Cube,new Vector3(-5.8f+i*.17f,2.5f,4.82f),new Vector3(.075f,4.7f,.12f),new Color(.43f,.32f,.22f));
            for(int i=0;i<7;i++) Shape("Floor seam",PrimitiveType.Cube,new Vector3(0,.008f,-5+i*1.5f),new Vector3(12,.008f,.012f),new Color(.19f,.23f,.24f));
            for(int i=0;i<3;i++){
                Shape("Window frame",PrimitiveType.Cube,new Vector3(5.84f,2.6f,-2+i*2.4f),new Vector3(.12f,2.7f,1.9f),new Color(.075f,.12f,.15f));
                var glass=Shape("Daylight panel",PrimitiveType.Cube,new Vector3(5.75f,2.6f,-2+i*2.4f),new Vector3(.035f,2.5f,1.7f),new Color(.55f,.78f,.83f));
                glass.GetComponent<Renderer>().material.EnableKeyword("_EMISSION");glass.GetComponent<Renderer>().material.SetColor("_EmissionColor",new Color(.25f,.4f,.45f));
            }
            for(int i=0;i<2;i++){
                Shape("Workbench pedestal",PrimitiveType.Cube,new Vector3(-.2f+i*3.6f,.4f,1.4f),new Vector3(.16f,.8f,1.65f),new Color(.08f,.13f,.16f));
                Shape("Ceiling light",PrimitiveType.Cube,new Vector3(-1+i*4,4.5f,.8f),new Vector3(2.3f,.05f,.2f),new Color(.92f,.88f,.73f));
            }
            Shape("Experiment mat",PrimitiveType.Cube,new Vector3(1.6f,.94f,1.4f),new Vector3(4.1f,.025f,1.7f),new Color(.055f,.12f,.14f));
            Shape("Bench edge",PrimitiveType.Cube,new Vector3(1.6f,.81f,.39f),new Vector3(4.5f,.045f,.025f),new Color(.21f,.66f,.63f));
            Shape("Lamp socket",PrimitiveType.Cylinder,new Vector3(3,1.02f,1.2f),new Vector3(.44f,.08f,.44f),new Color(.75f,.77f,.74f));
            Shape("Switch housing",PrimitiveType.Cube,new Vector3(1.6f,1.01f,.7f),new Vector3(.8f,.1f,.4f),new Color(.2f,.27f,.29f));
            Shape("Planter",PrimitiveType.Cylinder,new Vector3(4.8f,.35f,3.8f),new Vector3(.65f,.35f,.65f),new Color(.7f,.66f,.55f));
            for(int i=0;i<7;i++) {var leaf=Shape("Plant leaf",PrimitiveType.Sphere,new Vector3(4.8f+Mathf.Sin(i)*.25f,1+i*.09f,3.8f+Mathf.Cos(i)*.22f),new Vector3(.27f,.8f,.16f),new Color(.16f,.33f,.23f));leaf.transform.rotation=Quaternion.Euler(20*i,35*i,25);}
            batteryObject = Shape("Battery", PrimitiveType.Cylinder, new Vector3(.2f,1.2f,1.2f), new Vector3(.35f,.3f,.35f), new Color(.08f,.65f,.68f)).transform;
            Label("BATTERY", new Vector3(.2f,1.85f,1.2f), .035f);
            bulb = Shape("Lamp", PrimitiveType.Sphere, new Vector3(3,1.35f,1.2f), Vector3.one * .5f, Color.gray).GetComponent<Renderer>();
            lampObject = bulb.transform;
            Label("LAMP", new Vector3(3,1.85f,1.2f), .035f);
            lampLight = new GameObject("Lamp glow").AddComponent<Light>(); lampLight.type = LightType.Point; lampLight.color = new Color(1,.8f,.3f); lampLight.range = 3; lampLight.intensity = 3; lampLight.transform.position = new Vector3(3,1.7f,1.2f);
            switchLever = Shape("Switch", PrimitiveType.Cube, new Vector3(1.6f,1.1f,.7f), new Vector3(.6f,.09f,.18f), new Color(1,.55f,.25f)).transform;
            Label("SWITCH", new Vector3(1.6f,1.85f,.7f), .035f);
            Shape("Front wire", PrimitiveType.Cube, new Vector3(1.6f,.97f,.7f), new Vector3(2.8f,.035f,.035f), Color.cyan);
            Shape("Return wire", PrimitiveType.Cube, new Vector3(1.6f,.97f,1.7f), new Vector3(2.8f,.035f,.035f), Color.cyan);
            for (int i=0;i<2;i++) Shape("Wire side", PrimitiveType.Cube, new Vector3(.2f+i*2.8f,.97f,1.2f), new Vector3(.035f,.035f,1), Color.cyan);
        }
    }
}
