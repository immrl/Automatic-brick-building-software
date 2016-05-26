using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using AssemblyCSharp;
using System.Threading;

public class Voxelizer:MonoBehaviour{
	public Transform Canvas;
	public Transform LoadingBar;
	public Transform percentText;
	public Transform LoadingText;

	private Grid[,,] grids ;
	private Thread t1;

	float max_x;
	float min_x;
	
	float max_y;
	float min_y;
	
	float min_z;
	float max_z;
	float gridsize;
	int totalX;
	int totalY;
	int totalZ;

	private List<Grid> voxels;
	private Dictionary<ColorSpecification,List<Vector3>>voxelsCoordinate;
	private VoxelBuffer buffer;
	private bool onLoading = false;
	private bool inVoxelized = false;
	private Texture2D texture_black;
	private Texture2D texture_white;
	private float percentage;

	public void Start(){
		Canvas.gameObject.SetActive(false);
		LoadingBar.GetComponent<Image> ().fillAmount = 0;
	}

	public void FixedUpdate(){
		//Update GUI
		if (!inVoxelized)
			return;
		if (onLoading) {
			// Voxelizing...
			//update GUI initialize loading bar
			if (percentage < 1) {
				//Update the loading percentage(text part)
				percentText.GetComponent<Text> ().text = String.Format("{0:0.00}",percentage*100) + "%";
				LoadingText.gameObject.SetActive (true);
			} else {
				//update UI when the process is done
				LoadingText.gameObject.SetActive (true);
				percentText.GetComponent<Text> ().text = "DONE!";
			}
			//set the loading bar to active
			Canvas.gameObject.SetActive (true);
			//Update the loading percentage(Graphic part)
			LoadingBar.GetComponent<Image> ().fillAmount = percentage;
		} else {
			//no voxelizing... turn the loading bar to invisible
			Canvas.gameObject.SetActive (false);
		}

	}

	//A function calculate the incenter of a triangle (given three vertex point)
	//For Texture mapping, use for getting the color of the triangle 
	private Vector2 getIncenter(Vector2 uv1, Vector2 uv2, Vector2 uv3){
		float tir_a = Vector2.Distance(uv1,uv2);
		float tir_b = Vector2.Distance(uv2,uv3);
		float tir_c = Vector2.Distance(uv3,uv1);
		if (tir_a + tir_b + tir_c == 0) {
			return new Vector2(uv1.x, uv1.y);
		}
		float newU = (tir_a*uv1.x+tir_b*uv2.x+tir_c*uv3.x)/(tir_a+tir_b+tir_c);
		float newV = (tir_a*uv1.y+tir_b*uv2.y+tir_c*uv3.y)/(tir_a+tir_b+tir_c);
		return new Vector2(newU, newV);
	}

	// Check whether the giving distance could be within a voxel 
	private bool isWithinDistance (Vector3 a, Vector3 b){
		float xdistance = Mathf.Abs(a.x - b.x);
		float ydistance = Mathf.Abs(a.y - b.y);
		float zdistance = Mathf.Abs(a.z - b.z);
		if (xdistance > gridsize + float.Epsilon) {
			return false;
		}
		if (ydistance > gridsize/2.5f+ float.Epsilon) {
			return false;
		}
		if (zdistance > gridsize+ float.Epsilon) {
			return false;
		}
		return true;
	}

	//add points to grid
	private void addpt(Vector3 a, Vector3 b, Vector3 na, Vector3 nb, Color color){
		Vector3[] ptx = {a,b};
		Vector3[] nptx = {na,nb};

		int i = 0;
		foreach(Vector3 pt in ptx){
			//calculate the closest grid array index of each point
			int ptX = Mathf.RoundToInt(((pt.x-min_x)/(max_x-min_x))*totalX);
			int ptY = Mathf.RoundToInt(((pt.y-min_y)/(max_y-min_y))*totalY);
			int ptZ = Mathf.RoundToInt(((pt.z-min_z)/(max_z-min_z))*totalZ);
			if(grids[ptX,ptY,ptZ] == null){
				//using Min-Max Normalization
				//calculate the world coordinate of the center of the grid
				float realX = ((float)ptX /(float)totalX)*(max_x-min_x)+min_x;
				float realY = ((float)ptY /(float)totalY)*(max_y-min_y)+min_y;
				float realZ = ((float)ptZ /(float)totalZ)*(max_z-min_z)+min_z;
				
				grids[ptX,ptY,ptZ] = new Grid(realX,realY,realZ);
				grids[ptX,ptY,ptZ].setGPosition(ptX,ptY,ptZ);
			}
			grids[ptX,ptY,ptZ].addPoint(pt,nptx[i]);
			grids[ptX,ptY,ptZ].AddColor(pt,color);
			i++;
		}
	}

	public IEnumerator voxelize () {
		percentage = 0;
		transform.localScale = new Vector3(1.0f,1.0f,1.0f);
		inVoxelized = true;
		//get the mesh from OBJ
		GameObject target = GameObject.Find ("Target");
		OBJ obj = target.GetComponent<OBJ> ();
		// initiallize a grid list for rendering <- For debugging voxelization 
		voxels = new List<Grid>();
		// initiallize a map mapping list of grid's position and grid's color <- For debugging voxelization
		voxelsCoordinate = new Dictionary<ColorSpecification,List<Vector3>>();

		// Tell GUI that voxelization start -> load progress bar
		onLoading = true;
		obj.onProcessing = true;

		//get Gameobjects from OBJ
		GameObject[] gs = obj.ms;

		for (int a = 0; a < gs.Length; a++) {
			int steps;
			//get mesh from the Game object
			Mesh m = (gs[a].GetComponent(typeof(MeshFilter)) as MeshFilter).mesh;
			//get the mesh render from game object <- for getting color
			MeshRenderer mr = gs[a].GetComponent(typeof(MeshRenderer)) as MeshRenderer;

			//get the bounding box information of the mesh
			max_x = m.bounds.max.x;
			min_x = m.bounds.min.x;

			max_y = m.bounds.max.y;
			min_y = m.bounds.min.y;
			
			min_z = m.bounds.min.z;
			max_z = m.bounds.max.z;

			//set total number of grid to sampling
			//super rsolution: 4000000 (16GB required)
			//high resolution: 1000000	(8GB required)
			//normal resolution: 250000
			//low resolution: 62500
			//very low resolution: 15625 (recommended for ,debug use)
			int totalgrid = OBJ.sampleResoultion;

			//calculate the grid size
			float ny = (max_x - min_x)/((max_y - min_y)*2.5f);
			float nz = (max_x - min_x)/(max_z - min_z);
			totalX = Mathf.RoundToInt(Mathf.Pow(totalgrid * ny * nz,1.0f/3.0f));
			gridsize = (float)totalX;
			gridsize = (max_x - min_x)/gridsize;

			/*------------------create self-designed cooridinate------------------*/
			//using Min-Max Normalization
			//calculate the total number of grids in term of the three axis
			totalX = Mathf.RoundToInt((max_x - min_x)/gridsize);
			totalY = Mathf.RoundToInt((max_y - min_y)/(gridsize/2.5f));
			totalZ = Mathf.RoundToInt((max_z - min_z)/gridsize);
			totalgrid = (totalX+1)*(totalY+1)*(totalZ+1);

			//3D array to save the information of each grid
			grids = new Grid[totalX+1,totalY+1,totalZ+1];


			/*------------------Assign point to grid------------------*/

			int[] meshTriangles;
			float totalstep = 0;
			Color[] pixs = {};

			for(int num_mesh = 0; num_mesh < m.subMeshCount; num_mesh++){
				//get vertice list and uv list form mesh
				Vector3[] meshVertices = m.vertices;
				Vector2[] meshUV = m.uv;
				Vector3[] meshNormals = m.normals;
				//get triangles list form mesh
				meshTriangles = m.GetTriangles(num_mesh);
				totalstep = meshTriangles.Length/3.0f;

				//use for record the progress
				float stepCounter = 0;
				float successTriangle = 0;

				//get the color of the marterial
				Color materialcolor = mr.materials[num_mesh].color;
				//get the texture of the submesh
				Texture tex = mr.materials[num_mesh].GetTexture("_MainTex");
				Texture2D sourceTex = null;
				int sourceHeight = 0;
				int sourceWidth = 0;
				if(tex!=null){
					sourceTex = (tex as Texture2D);
					// load the whole texutre to array for quicker performance
					pixs = sourceTex.GetPixels(0, 0, sourceTex.width , sourceTex.height);
					sourceHeight = sourceTex.height;
					sourceWidth = sourceTex.width;
				}

				//prepare list for calculation
				List<int> tTriangles = new List<int>();
				List<int> ttTriangles = new List<int>();

				List<Vector2> tuvs = new List<Vector2>();
				List<Vector2> ttuvs = new List<Vector2>();

				List<Vector3> tVertices = new List<Vector3>();
				List<Vector3> ttVertices = new List<Vector3>();

				List<Vector3> tNormals = new List<Vector3>();
				List<Vector3> ttNormals = new List<Vector3>();
				//Loop for subdivide triangles, to ensure distances between vertices is within a grid
				while(meshTriangles.Length != 0){
					// update the GUI information
					LoadingText.GetComponent<Text> ().text = "Voxelization("+(num_mesh+1)+"/"+m.subMeshCount+") \n subdividing...";
					steps = 0;
					for (int i = 0; i < meshTriangles.Length; i += 3)
					{
						//get the three vertice point of each triangle
						Vector3 p1 = meshVertices[meshTriangles[i + 0]];
						Vector3 p2 = meshVertices[meshTriangles[i + 1]];
						Vector3 p3 = meshVertices[meshTriangles[i + 2]];

						Vector3 n1 = meshNormals[meshTriangles[i + 0]];
						Vector3 n2 = meshNormals[meshTriangles[i + 1]];
						Vector3 n3 = meshNormals[meshTriangles[i + 2]];

						Vector2 uv1 = new Vector2(0,1);
						Vector2 uv2 = new Vector2(0,1);
						Vector2 uv3 = new Vector2(0,1);
						if(sourceTex!=null){
							uv1 = meshUV[meshTriangles[i+0]];
							uv2 = meshUV[meshTriangles[i+1]];
							uv3 = meshUV[meshTriangles[i+2]];
							//convert UV within range 0-1 (solve out of range problem caused by wrapping)
							uv1 = new Vector2(uv1.x < 0 ? uv1.x+1-((int)uv1.x) : uv1.x, 
							                  uv1.y < 0 ? uv1.y+1-((int)uv1.y) : uv1.y);
							uv2 = new Vector2(uv2.x < 0 ? uv2.x+1-((int)uv2.x) : uv2.x, 
							                  uv2.y < 0 ? uv2.y+1-((int)uv2.y) : uv2.y);
							uv3 = new Vector2(uv3.x < 0 ? uv3.x+1-((int)uv3.x) : uv3.x,
							                  uv3.y < 0 ? uv3.y+1-((int)uv3.y) : uv3.y);
							
							uv1 = new Vector2(uv1.x > 1 ? uv1.x-((int)uv1.x) : uv1.x, 
							                  uv1.y > 1 ? uv1.y-((int)uv1.y) : uv1.y);
							uv2 = new Vector2(uv2.x > 1 ? uv2.x-((int)uv2.x) : uv2.x,
							                  uv2.y > 1 ? uv2.y-((int)uv2.y) : uv2.y);
							uv3 = new Vector2(uv3.x > 1 ? uv3.x-((int)uv3.x) : uv3.x,
							                  uv3.y > 1 ? uv3.y-((int)uv3.y) : uv3.y);
						}


						Vector3[] ptx = {p1,p2,p3};
						Vector3[] nptx = {n1,n2,n3};
						List<bool> valid = new List<bool>();
						int index = 0;

						for (int j =1; j <=3; j++) {
							if (!isWithinDistance(ptx[j%3],ptx[j-1])) {
								index = j;
								valid.Add(false);
							}
						}
						
						if (valid.Count > 1) {
							Vector3 ab = (p1+p2)/2;
							Vector3 ac = (p1+p3)/2;
							Vector3 bc = (p2+p3)/2;

							Vector3 nab = (n1+n2)/2;
							Vector3 nac = (n1+n3)/2;
							Vector3 nbc = (n2+n3)/2;

							ttVertices.Add(p1);
							ttVertices.Add(ab);
							ttVertices.Add(ac);
							ttNormals.Add(n1);
							ttNormals.Add(nab);
							ttNormals.Add(nac);
							ttTriangles.Add(ttVertices.Count-3);
							ttTriangles.Add(ttVertices.Count-2);
							ttTriangles.Add(ttVertices.Count-1);

							ttVertices.Add(ab);
							ttVertices.Add(p2);
							ttVertices.Add(bc);
							ttNormals.Add(nab);
							ttNormals.Add(n2);
							ttNormals.Add(nbc);
							ttTriangles.Add(ttVertices.Count-3);
							ttTriangles.Add(ttVertices.Count-2);
							ttTriangles.Add(ttVertices.Count-1);

							ttVertices.Add(ab);
							ttVertices.Add(bc);
							ttVertices.Add(ac);
							ttNormals.Add(nab);
							ttNormals.Add(nbc);
							ttNormals.Add(nac);
							ttTriangles.Add(ttVertices.Count-3);
							ttTriangles.Add(ttVertices.Count-2);
							ttTriangles.Add(ttVertices.Count-1);

							ttVertices.Add(ac);
							ttVertices.Add(p3);
							ttVertices.Add(bc);
							ttNormals.Add(nac);
							ttNormals.Add(n3);
							ttNormals.Add(nbc);
							ttTriangles.Add(ttVertices.Count-3);
							ttTriangles.Add(ttVertices.Count-2);
							ttTriangles.Add(ttVertices.Count-1);

							if(sourceTex!=null){
								Vector2 uvab = (uv1+uv2)/2;
								Vector2 uvac = (uv1+uv3)/2;
								Vector2 uvbc = (uv2+uv3)/2;
								ttuvs.Add(uv1);
								ttuvs.Add(uvab);
								ttuvs.Add(uvac);

								ttuvs.Add(uvab);
								ttuvs.Add(uv2);
								ttuvs.Add(uvbc);

								ttuvs.Add(uvab);
								ttuvs.Add(uvbc);
								ttuvs.Add(uvac);

								ttuvs.Add(uvac);
								ttuvs.Add(uv3);
								ttuvs.Add(uvbc);
							}
							totalstep+=3;

						}else if(valid.Count == 0){
							tVertices.Add(p1);
							tVertices.Add(p2);
							tVertices.Add(p3);
							tNormals.Add(n1);
							tNormals.Add(n2);
							tNormals.Add(n3);
							if(sourceTex!=null){
								tuvs.Add(uv1);
								tuvs.Add(uv2);
								tuvs.Add(uv3);
							}
							tTriangles.Add(tVertices.Count-3);
							tTriangles.Add(tVertices.Count-2);
							tTriangles.Add(tVertices.Count-1);

							successTriangle++;

						}else if(valid.Count == 1){
							if(index == 0){
								Debug.Log (index);
								yield break;
							}
							int index2 = index -1;
							index = index%3;
							p1 = ptx[index];
							p2 = ptx[index2];
							p3 = ptx[3-(index+index2)];

							n1 = nptx[index];
							n2 = nptx[index2];
							n3 = nptx[3-(index+index2)];

							Vector3 midpt = (p1+p2)/2;
							Vector3 midnor = (n1+n2)/2;

							ttVertices.Add(p1);
							ttVertices.Add(midpt);
							ttVertices.Add(p3);
							ttNormals.Add(n1);
							ttNormals.Add(midnor);
							ttNormals.Add(n3);
							ttTriangles.Add(ttVertices.Count-3);
							ttTriangles.Add(ttVertices.Count-2);
							ttTriangles.Add(ttVertices.Count-1);
							
							ttVertices.Add(p2);
							ttVertices.Add(midpt);
							ttVertices.Add(p3);
							ttNormals.Add(n2);
							ttNormals.Add(midnor);
							ttNormals.Add(n3);
							ttTriangles.Add(ttVertices.Count-3);
							ttTriangles.Add(ttVertices.Count-2);
							ttTriangles.Add(ttVertices.Count-1);

							if(sourceTex!=null){
								Vector2[] tempUv = {uv1,uv2,uv3};
								uv1 = tempUv[index];
								uv2 = tempUv[index2];
								uv3 = tempUv[3-(index+index2)];

								Vector2 miduv = (uv1+uv2)/2;

								ttuvs.Add(uv1);
								ttuvs.Add(miduv);
								ttuvs.Add(uv3);

								ttuvs.Add(uv2);
								ttuvs.Add(miduv);
								ttuvs.Add(uv3);
							}

							totalstep++;
						}

						steps++;
						valid.Clear();
						percentage = successTriangle / totalstep;

						//every 6000 steps (max: 9000)
						//pause the function and return the control of OS
						if(steps > 6000){
							steps = 0;
							yield return new WaitForFixedUpdate();
						}
					}/*end of for-loop*/

					meshVertices = ttVertices.ToArray();
					if(sourceTex!=null){
						meshUV = ttuvs.ToArray();
					}
					meshTriangles = ttTriangles.ToArray();
					meshNormals = ttNormals.ToArray();

					ttVertices.Clear();
					ttNormals.Clear ();
					if(sourceTex!=null){
						ttuvs.Clear();
					}
					ttTriangles.Clear();

					stepCounter++;
				} /*end of while loop of subdivision triangle*/

				steps = 0;
				LoadingText.GetComponent<Text> ().text = "Voxelization("+(num_mesh+1)+"/"+m.subMeshCount+") \n Voxelizing...";
				//Debug.Log (stepCounter);
				//Debug.Log (tTriangles.Count/3);

				DateTime beforeTime = DateTime.Now;
				//Add every point to correspoinding grid with color
				for (int i = 0; i < tTriangles.Count; i += 3){

					Vector3 p1 = tVertices[tTriangles[i + 0]];
					Vector3 p2 = tVertices[tTriangles[i + 1]];
					Vector3 p3 = tVertices[tTriangles[i + 2]];

					Vector3 n1 = tNormals[tTriangles[i + 0]];
					Vector3 n2 = tNormals[tTriangles[i + 1]];
					Vector3 n3 = tNormals[tTriangles[i + 2]];

					Vector2 uv1;
					Vector2 uv2;
					Vector2 uv3;

					Color averageC = new Color(0,0,0,0);

					if(sourceTex!=null){
						uv1 = tuvs[tTriangles[i+0]];
						uv2 = tuvs[tTriangles[i+1]];
						uv3 = tuvs[tTriangles[i+2]];
						// sample color from the incenter of the triangle
						Vector2 incenter = getIncenter(uv1,uv2,uv3);

						int sampleX = Mathf.RoundToInt(incenter.x*(sourceWidth-1));
						int sampleY = Mathf.RoundToInt(incenter.y*(sourceHeight-1));
						averageC = pixs[(sampleY*sourceWidth)+sampleX]*materialcolor;
					}else{
						averageC = materialcolor;
					}

					//add point to the grid
					addpt(p1,p2,n1,n2,averageC);
					addpt(p1,p3,n1,n3,averageC);
					addpt(p2,p3,n2,n3,averageC);

					steps+=3;
					percentage = ((float)steps)/tTriangles.Count;
					///*pause the function and return control to OS every 4000 steps
					if(steps % 12000 == 0){	//max: 18000
						yield return new WaitForFixedUpdate();
					}//*/
				}
				DateTime afterTime = DateTime.Now;
				Debug.Log ((afterTime.Ticks - beforeTime.Ticks )/10000);
				tVertices.Clear();
				tTriangles.Clear();
				tuvs.Clear();
			}/*end of voxelization*/
			Debug.Log ("getpixels");
			Debug.Log ("end of voxelize");

			//turn the original 3D object invisible
			(gs[a].GetComponent(typeof(MeshRenderer))as MeshRenderer).enabled = false;

			/*------------------render voxels------------------ Add (* /) to uncomment the code-->>*/

			//Debug use which render each voxels in the game environment
			
			yield return new WaitForFixedUpdate();
			int counter = 0;
			
			for (int c= 0; c < (int)ColorSpecification.NUM_OF_COLOR; c++){
				voxelsCoordinate.Add((ColorSpecification)c,new List<Vector3>());
			}

			foreach(Grid grid in grids){
				if(grid != null){
					voxels.Add(grid);
				}
			}

			foreach(Grid grid in voxels){
				voxelsCoordinate[grid.nearestColor()].Add(grid.getPosition());
				counter++;
			}

			int max_vertice_gameObject = 2500;
			buffer = new VoxelBuffer();

			for (int c= 0; c < (int)ColorSpecification.NUM_OF_COLOR; c++){

				List<Vector3> coor = voxelsCoordinate[(ColorSpecification)c];

				if(coor.Count>0){
					List<GameObject> gos = new List<GameObject>();
					int index = 0;
					Color color;
					color = Grid.getColor((ColorSpecification)c);
					for(int i = coor.Count ; i > 0; i-=max_vertice_gameObject){
						GameObject go = new GameObject();
						go.transform.parent = GameObject.Find("Voxelized").transform;
						go.AddComponent(typeof(MeshFilter));
						go.AddComponent(typeof(MeshRenderer));
						gos.Add(go);
					}
					
					for(int i =0; i < gos.Count; i++){
						int count_coor = (coor.Count - i*max_vertice_gameObject >=max_vertice_gameObject)? max_vertice_gameObject : coor.Count - i*max_vertice_gameObject;
						buffer.draw(gos[i],coor.GetRange(i*max_vertice_gameObject,count_coor),gridsize,color);
						index++;
					}
					gos.Clear();
				}
			}

			Debug.Log (voxels.Count);
			voxelsCoordinate.Clear();

			/* Testing the lxfmlWriter
			LxfmlWriter xmlWriter = new LxfmlWriter();
			xmlWriter.writeXML(voxels);
			*/
			/*
			for (int x = 0 ; x < totalX+1;x++){
				for (int y = 0 ; y < totalY+1;y++){
					for (int z = 0 ; z < totalZ+1;z++){

					}
				}
			}
			*/
			foreach(Grid grid in grids){
				if(grid != null){
					voxels.Add(grid);
				}
			}
			Debug.Log (voxels.Count);

			voxels.Clear();

			Debug.Log (voxels.Count);


			int[] colorsFreq = new int[(int)ColorSpecification.NUM_OF_COLOR];
			int[] allColorsFreq = new int[(int)ColorSpecification.NUM_OF_COLOR];
			float thesholdDegree = 0;

			LoadingText.GetComponent<Text> ().text = "Legoization(Filling)";
			percentage = 0;
			steps = 0;

			for (int y = 0 ; y < totalY+1;y++){
				for (int z = 0 ; z < totalZ+1;z++){
					for (int x = 0 ; x < totalX+1;x++){
						//fill voxels
						for (int i = 0; i < (int)ColorSpecification.NUM_OF_COLOR; i++){
							colorsFreq[i] = 0;
							allColorsFreq[i] = 0;
						}

						int index = 1;
						int face = 0;
						bool[] keepGo = {true, true, true, true, true, true};
						bool getColor = true;
						bool endFlag = false;
						while(face < 6 && !endFlag ){
							if(keepGo[0]){
								if(x+index > totalX)
									endFlag = true;
								if(!endFlag && grids[x+index,y,z] != null && grids[x+index,y,z].isBound){
									if(getColor)
										colorsFreq[(int)grids[x+index,y,z].nearestColor()]++;
									allColorsFreq[(int)grids[x+index,y,z].nearestColor()]++;
									face++;
									getColor = false;
									keepGo[0] = false;
								}
							}
							if(keepGo[1]){
								if(x-index < 0)
									endFlag = true;
								if(!endFlag && grids[x-index,y,z] != null && grids[x-index,y,z].isBound){
									if(getColor)
										colorsFreq[(int)grids[x-index,y,z].nearestColor()]++;
									allColorsFreq[(int)grids[x-index,y,z].nearestColor()]++;
									face++;
									getColor = false;
									keepGo[1] = false;
								}
							}

							if(keepGo[2]){
								if(y+index > totalY)
									endFlag = true;
								if(!endFlag && grids[x,y+index,z] != null && grids[x,y+index,z].isBound){
									if(getColor)
										colorsFreq[(int)grids[x,y+index,z].nearestColor()]++;
									allColorsFreq[(int)grids[x,y+index,z].nearestColor()]++;
									face++;
									getColor = false;
									keepGo[2] = false;
								}
							}
							if(keepGo[3]){
								if(y-index < 0)
									endFlag = true;
								if(!endFlag && grids[x,y-index,z] != null && grids[x,y-index,z].isBound){
									if(getColor)
										colorsFreq[(int)grids[x,y-index,z].nearestColor()]++;
									allColorsFreq[(int)grids[x,y-index,z].nearestColor()]++;
									face++;
									getColor = false;
									keepGo[3] = false;
								}
							}

							if(keepGo[4]){
								if(z+index > totalZ)
									endFlag = true;
								if(!endFlag && grids[x,y,z+index] != null && grids[x,y,z+index].isBound){
									if(getColor)
										colorsFreq[(int)grids[x,y,z+index].nearestColor()]++;
									allColorsFreq[(int)grids[x,y,z+index].nearestColor()]++;
									face++;
									getColor = false;
									keepGo[4] = false;
								}
							}
							if(keepGo[5]){
								if(z-index < 0)
									endFlag = true;
								if(!endFlag && grids[x,y,z-index] != null && grids[x,y,z-index].isBound){
									if(getColor)
										colorsFreq[(int)grids[x,y,z-index].nearestColor()]++;
									allColorsFreq[(int)grids[x,y,z-index].nearestColor()]++;
									face++;
									getColor = false;
									keepGo[5] = false;
								}
							}
							index++;
						}

						index = 0;
						int minFreq = int.MaxValue;
						int minIndex = -1;

						if(!endFlag){
							foreach(int freq in colorsFreq){
								if(freq > 0){
									if(freq < minFreq){
										minFreq = freq;
										minIndex = index;
									}
								}
								index++;
							}

							int numOfColor = 0;
							foreach(int freq in colorsFreq){
								if(freq == minFreq)
									numOfColor++;
							}

							if(numOfColor > 1){
								minFreq = int.MaxValue;
								minIndex = -1;
								foreach(int freq in allColorsFreq){
									if(freq > 0){
										if(freq < minFreq){
											minFreq = freq;
											minIndex = index;
										}
									}
									index++;
								}
							}
						}

						if(minIndex != -1){
							float realX = ((float)x /(float)totalX)*(max_x-min_x)+min_x;
							float realY = ((float)y /(float)totalY)*(max_y-min_y)+min_y;
							float realZ = ((float)z /(float)totalZ)*(max_z-min_z)+min_z;

							grids[x,y,z] = new Grid(realX,realY,realZ,false);
							grids[x,y,z].setGPosition(x,y,z);
							grids[x,y,z].AddColor(Grid.getColor((ColorSpecification)minIndex));
						}

						steps++;
						if(steps % 10000 == 0){	//max: 18000
							percentage = ((float)steps)/((totalX+1)*(totalY+1)*(totalZ+1));
							yield return new WaitForFixedUpdate();
						}

					}
				}
			}

			foreach(Grid grid in grids){
				if(grid != null){
					voxels.Add(grid);
				}
			}
			Debug.Log (voxels.Count);

			//remove lists to release memory
			voxels.Clear();
			System.GC.Collect();
			Resources.UnloadUnusedAssets();

			//update the UI indicate the voxelization has done
			onLoading = false;
		}

		yield return new WaitForFixedUpdate();
		inVoxelized = false;

		transform.localScale = new Vector3(-1.0f,1.0f,1.0f);

		///*call legoize process
		Legoizer legoizer = GameObject.Find ("Legoizer").GetComponent<Legoizer> ();
		StartCoroutine (legoizer.Legoize(grids,new Vector3(totalX+1, totalY+1, totalZ+1),gridsize));
		//*/
	}
}
