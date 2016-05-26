using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
//using UnityEditor;
using System.Windows.Forms;
using System.Runtime.InteropServices;

using System.Text.RegularExpressions;
using AssemblyCSharp;

public class OBJ : MonoBehaviour {
	
	string objPath = "";
	public Transform actionCam;
	public GameObject[] ms;

	public Transform Canvas;
	public Transform LoadingBar;
	public Transform percentText;
	public Transform LoadingText;

	/* OBJ file tags */
	private const string O 	= "o";
	private const string G 	= "g";
	private const string V 	= "v";
	private const string VT = "vt";
	private const string VN = "vn";
	private const string F 	= "f";
	private const string MTL = "mtllib";
	private const string UML = "usemtl";

	/* MTL file tags */
	private const string NML = "newmtl";
	private const string NS = "Ns"; // Shininess
	private const string KA = "Ka"; // Ambient component (not supported)
	private const string KD = "Kd"; // Diffuse component
	private const string KS = "Ks"; // Specular component
	private const string D = "d"; 	// Transparency (not supported)
	private const string TR = "Tr";	// Same as 'd'
	private const string ILLUM = "illum"; // Illumination model. 1 - diffuse, 2 - specular
	private const string MAP_KD = "map_Kd"; // Diffuse texture (other textures are not supported)

	private string basepath;
	private string mtllib;
	private GeometryBuffer buffer;
	private bool isObjectLoaded = false;
	public bool onProcessing = false;
	public bool hasLegoized = false;
	public List<Lego> legos;
	private string samplingResolutionStr = "1000000";
	public static int sampleResoultion = 1000000;
	public static int colorMode = 0;
	public static string[] colorModes = new string[] { "Nearest Distance", "Average Color" };

	System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

	public void Start(){
		//GCHandle handle = GCHandle.Alloc(new object(), GCHandleType.Weak);
		//IntPtr ptr = GCHandle.ToIntPtr(handle);
		//GCHandle testHandle = GCHandle.FromIntPtr(ptr);
	}

//	[DllImport("user32.dll")]
//	private static extern void OpenFileDialog(); //in your case : OpenFileDialog

	private void initalizeOpenFile(){
		openFileDialog.Filter = "Object files (*.obj)|*.obj|All files (*.*)|*.*" ;
		openFileDialog.InitialDirectory = UnityEngine.Application.dataPath;
		openFileDialog.Title = "Select Object";
	}

	public void OnGUI() {
		if (onProcessing) {
			return;
		}
		GUI.depth = 10;

		GUI.enabled = false;
		objPath = GUI.TextField(new Rect(10,10,300,20), objPath);		
		GUI.enabled = true;

		if (GUI.Button (new Rect (310, 10, 20, 20), "...")) {

			OpenFileName ofn = new OpenFileName();  

			ofn.structSize = Marshal.SizeOf(ofn);  

			ofn.filter = "Object Files (*.obj)\0*.obj\0\0";  

			ofn.file = new string(new char[256]);  

			ofn.maxFile = ofn.file.Length;  

			ofn.fileTitle = new string(new char[64]);  

			ofn.maxFileTitle = ofn.fileTitle.Length;  

			ofn.initialDir =UnityEngine.Application.dataPath;  

			ofn.title = "Select Object";  

			ofn.defExt = "OBJ";//显示文件的类型  

			ofn.flags=0x00080000|0x00001000|0x00000800|0x00000008;//OFN_EXPLORER|OFN_FILEMUSTEXIST|OFN_PATHMUSTEXIST| OFN_ALLOWMULTISELECT|OFN_NOCHANGEDIR  
			// ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000200 | 0x00000008;

			if(DllTest.GetOpenFileName( ofn ))  
			{  

				// StartCoroutine(WaitLoad(ofn.file));//加载图片到panle  
				objPath = ofn.file;
				Debug.Log( "Selected file with full path: "+ ofn.file );  

			}
			/*
			if(openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK){
				objPath = openFileDialog.FileName;
			}else{
				objPath="";
			}
			*/
			//objPath = EditorUtility.OpenFilePanel("Please select an object file","","obj");
			Debug.Log (objPath);
		}

		if (GUI.Button(new Rect(10,40,100,20), "import")) {
			hasLegoized = false;
			if(objPath==""){
				MessageBox.Show ("You Must Select an Object first!","Select Object");
				/*EditorUtility.DisplayDialog(
					"Select Object",
					"You Must Select an Object first!",
					"Ok");*/
				return;
			}else{
				isObjectLoaded = false;
				Destroy(GetComponent("MeshFilter"));
				Destroy(GetComponent("MeshRenderer"));
				Destroy(GetComponent("MeshCollider"));

				GameObject voxelContainer = GameObject.Find ("Voxelized");
				GameObject legoContainer = GameObject.Find ("Legoizer");
				StartCoroutine (removeVoxels (voxelContainer));
				StartCoroutine (removeVoxels (legoContainer));
				buffer = new GeometryBuffer ();
				mtllib="";
				StartCoroutine (Load ("file:///"+objPath));
			}
		}

		GUI.enabled = isObjectLoaded;

		GUI.Label(new Rect(10, 70, 220, 20), "Sample Resolution (1000-8000000): ");
		samplingResolutionStr = GUI.TextField(new Rect(230, 70, 100, 20), samplingResolutionStr, 25);
		samplingResolutionStr= Regex.Replace(samplingResolutionStr, "[^0-9]", "");
		sampleResoultion = int.Parse (samplingResolutionStr);
		
		colorMode = GUI.SelectionGrid(new Rect(10, 100, 220, 20), colorMode, colorModes, colorModes.Length, GUI.skin.toggle);

		if (GUI.Button (new Rect (10, 130, 100, 20), "Legoize")) {
			hasLegoized = false;
			if(sampleResoultion < 1001 || sampleResoultion > 8000000){
				MessageBox.Show ("The sample resolution must be in between 1000 to 8000000","Out of Range");
				/*EditorUtility.DisplayDialog(
					"Out of Range",
					"The sample resolution must be in between 1000 to 8000000",
					"Ok");*/
				return;
			}
			GameObject voxelContainer = GameObject.Find ("Voxelized");
			GameObject legoContainer = GameObject.Find ("Legoizer");

			while(voxelContainer.transform.childCount > 0)
			{
				GameObject.DestroyImmediate(voxelContainer.transform.GetChild(0).gameObject);
			}

			while(legoContainer.transform.childCount > 0)
			{
				GameObject.DestroyImmediate(legoContainer.transform.GetChild(0).gameObject);
			}

			Resources.UnloadUnusedAssets();
			System.GC.Collect();
			Voxelizer voxelizer = GameObject.Find ("Voxelized").GetComponent<Voxelizer> ();
			StartCoroutine (voxelizer.voxelize());
		}

		GUI.enabled = true;

		if (hasLegoized) {
			if (GUI.Button (new Rect (10, 160, 100, 20), "Save")) {
				LxfmlWriter xmlWriter = GameObject.Find ("FileWriter").GetComponent<LxfmlWriter> ();
				StartCoroutine(xmlWriter.writeXML(legos));
			}
		}

	}

	public void Update() {
		return;
		if (gameObject.GetComponent ("MeshFilter")as MeshFilter == null)
			return;
		Mesh mesh = (gameObject.GetComponent ("MeshFilter")as MeshFilter).mesh;
		if (mesh != null) {
			if(Vector3.Distance(mesh.bounds.max, mesh.bounds.min) > 0){
				//get the bounding box information of the mesh
				float max_x = mesh.bounds.max.x;
				float min_x = mesh.bounds.min.x;
				
				float max_y = mesh.bounds.max.y;
				float min_y = mesh.bounds.min.y;
				
				float min_z = mesh.bounds.min.z;
				float max_z = mesh.bounds.max.z;
				
				//set total number of grid to sampling
				//super rsolution: 8000000 (16GB required)
				//high resolution: 1000000	(8GB required)
				//normal resolution: 250000
				//low resolution: 62500
				//very low resolution: 15625 (recommended for debug use)
				int totalgrid = sampleResoultion;
				
				//calculate the grid size
				float ny = (max_x - min_x)/((max_y - min_y)*2.5f);
				float nz = (max_x - min_x)/(max_z - min_z);
				int totalX = Mathf.RoundToInt(Mathf.Pow(totalgrid * ny * nz,1.0f/3.0f));
				float gridsize = (float)totalX;
				gridsize = (max_x - min_x)/gridsize;

				for(float z = mesh.bounds.min.z; z < mesh.bounds.max.z;z+=gridsize){
					for(float x = mesh.bounds.min.x; x < mesh.bounds.max.x;x+=gridsize){
						Debug.DrawLine(new Vector3(x, mesh.bounds.min.y, z), new Vector3(x, mesh.bounds.max.y, z), Color.white);
						for(float y = mesh.bounds.min.y; y < mesh.bounds.max.y;y+=(gridsize/2.5f)){
							Debug.DrawLine(new Vector3(x, y, mesh.bounds.min.z), new Vector3(x, y, mesh.bounds.max.z), Color.white);
						}
					}

					for(float y = mesh.bounds.min.y; y < mesh.bounds.max.y;y+=(gridsize/2.5f)){
						Debug.DrawLine(new Vector3(mesh.bounds.min.x, y, z), new Vector3(mesh.bounds.max.x, y, z), Color.white);
					}
				}

			}
		}

	}

	public IEnumerator removeVoxels(GameObject voxelContainer){
		while(voxelContainer.transform.childCount > 0)
		{
			GameObject.DestroyImmediate(voxelContainer.transform.GetChild(0).gameObject);
		}
		Resources.UnloadUnusedAssets();
		System.GC.Collect();

		yield return new WaitForFixedUpdate();
	}

	public IEnumerator Load(string path) {
		basepath = (path.IndexOf("\\") == -1) ? "" : path.Substring(0, path.LastIndexOf("\\") + 1);
		WWW loader = new WWW(path);
		yield return loader;
		SetGeometryData(loader.text);

		if(hasMaterials) {
			loader = new WWW(basepath + mtllib);
			yield return loader;
			SetMaterialData(loader.text);

			foreach(MaterialData m in materialData) {
				if(m.diffuseTexPath != null) {
					Debug.Log (basepath + m.diffuseTexPath);
					WWW texloader = new WWW(basepath + m.diffuseTexPath);
					yield return texloader;
					m.diffuseTex = texloader.texture;
				}
			}
		}
		Build();
		yield return new WaitForFixedUpdate();
		isObjectLoaded = true;
	}

	private void SetGeometryData(string data) {
		string[] lines = data.Split("\n".ToCharArray());
		
		for(int i = 0; i < lines.Length; i++) {
			string l = lines[i];
			l = l.Replace("  ", " |").Replace("| ", "").Replace("|", "");
			if(l.IndexOf("#") != -1) l = l.Substring(0, l.IndexOf("#"));
			string[] p = l.Split(" ".ToCharArray());
			switch(p[0]) {
				case O:
					//buffer.PushObject(p[1].Trim());
					break;
				case G:
					buffer.PushGroup(p[1].Trim());
					break;
				case V:
					buffer.PushVertex( new Vector3( cf(p[1]), cf(p[2]), cf(p[3]) ) );
					break;
				case VT:
					buffer.PushUV(new Vector2( cf(p[1]), cf(p[2]) ));
					break;
				case VN:
					buffer.PushNormal(new Vector3( cf(p[1]), cf(p[2]), cf(p[3]) ));
					break;
				case F:
					for(int j = 1; j < p.Length; j++) {
						string[] c = p[j].Trim().Split("/".ToCharArray());
						FaceIndices fi = new FaceIndices();
						if(c[0] != "")
							fi.vi = ci(c[0])-1;	
						if(c.Length > 1 && c[1] != "") fi.vu = ci(c[1])-1;
						if(c.Length > 2 && c[2] != "") fi.vn = ci(c[2])-1;
						if(c[0] != "")	
							buffer.PushFace(fi);
					}
					break;
				case MTL:
					mtllib = p[1].Trim();
					break;
				case UML:
					buffer.PushMaterialName(p[1].Trim());
					break;
			}
		}
		
		 //buffer.Trace();
	}
	
	private float cf(string v) {
		return Convert.ToSingle(v.Trim(), new CultureInfo("en-US"));
	}
	
	private int ci(string v) {
		return Convert.ToInt32(v.Trim(), new CultureInfo("en-US"));
	}
	
	private bool hasMaterials {
		get {
			return mtllib != null;
		}
	}
	
	/* ############## MATERIALS */
	private List<MaterialData> materialData;
	private class MaterialData {
		public string name;
		public Color ambient;
   		public Color diffuse;
   		public Color specular;
   		public float shininess;
   		public float alpha;
   		public int illumType;
   		public string diffuseTexPath;
   		public Texture2D diffuseTex;
	}
	
	private void SetMaterialData(string data) {
		string[] lines = data.Split("\n".ToCharArray());
		
		materialData = new List<MaterialData>();
		MaterialData current = new MaterialData();
		
		for(int i = 0; i < lines.Length; i++) {
			string l = lines[i];
			if(l.IndexOf("#") != -1) l = l.Substring(0, l.IndexOf("#"));
			string[] p = l.Split(" ".ToCharArray());
			switch(p[0].Trim()) {
				case NML:
					current = new MaterialData();
					current.name = p[1].Trim();
					materialData.Add(current);
					break;
				case KA:
					current.ambient = gc(p);
					break;
				case KD:
					current.diffuse = gc(p);
					break;
				case KS:
					current.specular = gc(p);
					break;
				case NS:
					current.shininess = cf(p[1]) / 1000;
					break;
				case D:
				case TR:
					current.alpha = cf(p[1]);
					break;
				case MAP_KD:
					current.diffuseTexPath = p[1].Trim();
					break;
				case ILLUM:
					current.illumType = ci(p[1]);
					break;
					
			}
		}	
	}
	
	private Material GetMaterial(MaterialData md) {
		Material m;
		
		if(md.illumType == 2) {
			m =  new Material(Shader.Find("Specular"));//Specular
			m.SetColor("_SpecColor", md.specular);
			m.SetFloat("_Shininess", md.shininess);
		} else {
			m =  new Material(Shader.Find("Diffuse"));
		}

		m.SetColor("_Color", md.diffuse);
		
		if(md.diffuseTex != null) m.SetTexture("_MainTex", md.diffuseTex);
		
		return m;
	}
	
	private Color gc(string[] p) {
		return new Color( cf(p[1]), cf(p[2]), cf(p[3]) );
	}

	private void Build() {
		Dictionary<string, Material> materials = new Dictionary<string, Material>();

		if(hasMaterials) {
			foreach(MaterialData md in materialData) {
				materials.Add(md.name, GetMaterial(md));
			}
		} else {
			materials.Add("default", new Material(Shader.Find("Diffuse")));
		}

		ms = new GameObject[buffer.numObjects];
		
		if(buffer.numObjects == 1) {
			gameObject.AddComponent(typeof(MeshFilter));
			gameObject.AddComponent(typeof(MeshRenderer));
			ms[0] = gameObject;
		} else if(buffer.numObjects > 1) {
			for(int i = 0; i < buffer.numObjects; i++) {
				GameObject go = new GameObject();
				go.transform.parent = gameObject.transform;
				go.AddComponent(typeof(MeshFilter));
				go.AddComponent(typeof(MeshRenderer));
				ms[i] = go;
			}
		}
		buffer.PopulateMeshes(ms, materials, actionCam);
	}
}

