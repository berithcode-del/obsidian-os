import * as THREE from './assets/three-r160.module.js';

const canvas = document.querySelector('#weather-canvas');
const reduced = matchMedia('(prefers-reduced-motion: reduce)');
try {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false, powerPreference: 'low-power' });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  const scene = new THREE.Scene();
  const sunny = new THREE.Color('#91ddf2'), storm = new THREE.Color('#5a7181');
  scene.background = sunny.clone();
  scene.fog = new THREE.Fog('#91ddf2', 30, 65);
  const camera = new THREE.PerspectiveCamera(42, 1, .1, 100);
  camera.position.set(0, 7.2, 25); camera.lookAt(0, 4.2, 0);
  const ambient = new THREE.HemisphereLight(0xe9faff, 0x558e3d, 2.2); scene.add(ambient);
  const sunLight = new THREE.DirectionalLight(0xffedc3, 2.2); sunLight.position.set(-9, 15, 10); scene.add(sunLight);
  const mat = (color) => new THREE.MeshStandardMaterial({ color, roughness: 1, flatShading: true });
  const grass = mat('#7bcf68'), hillMat = [mat('#63bb59'), mat('#8cdb72'), mat('#56ae50')];
  const ground = new THREE.Mesh(new THREE.PlaneGeometry(90, 70), grass); ground.rotation.x = -Math.PI / 2; ground.position.y = -1.2; scene.add(ground);
  const hillGeo = new THREE.SphereGeometry(1, 18, 12);
  [[-18,-1,-22,13,5],[-7,-2,-25,16,6],[9,-2,-24,15,5],[21,-1,-20,12,5],[-24,-2,-10,13,4],[23,-2,-9,12,4]].forEach(([x,y,z,w,h], i) => { const hill = new THREE.Mesh(hillGeo, hillMat[i % hillMat.length]); hill.position.set(x,y,z); hill.scale.set(w,h,6); scene.add(hill); });
  const trunkMat = mat('#a67042'), leafMat = [mat('#56b75b'),mat('#73cc61'),mat('#46a751')];
  const trunkGeo = new THREE.CylinderGeometry(.14,.2,1.6,7), foliageGeo = new THREE.SphereGeometry(1,8,6);
  [[-15,-6,.9],[-12,-13,1.1],[-20,-17,1.5],[14,-12,1.2],[19,-8,1.05],[8,-20,.8],[-5,-21,.85],[24,-17,1.3]].forEach(([x,z,scale],i) => { const tree=new THREE.Group(); const trunk=new THREE.Mesh(trunkGeo,trunkMat);trunk.position.y=.1;tree.add(trunk);for(const [dx,dy,dz,r] of [[0,1.4,0,1.2],[-.7,1.1,0,.8],[.7,1.1,0,.85]]){const leaf=new THREE.Mesh(foliageGeo,leafMat[(i+Math.round(dx+1))%3]);leaf.position.set(dx,dy,dz);leaf.scale.setScalar(r);tree.add(leaf)}tree.position.set(x,-.4,z);tree.scale.setScalar(scale);scene.add(tree); });
  const cloudMaterial = mat('#fffdf0');
  const cloudGeo = new THREE.SphereGeometry(1, 9, 7); const clouds=[];
  [[-12,10,-15,1.4],[-3,12,-22,1.2],[9,10.5,-19,1.5],[21,12,-25,1.4]].forEach(([x,y,z,scale])=>{const group=new THREE.Group();[[-1.1,0,0,1],[0,.35,0,1.35],[1.25,0,0,1],[.4,-.12,.5,1]].forEach(([a,b,c,d])=>{const puff=new THREE.Mesh(cloudGeo,cloudMaterial);puff.position.set(a,b,c);puff.scale.setScalar(d);group.add(puff)});group.position.set(x,y,z);group.scale.setScalar(scale);scene.add(group);clouds.push(group)});
  const sun = new THREE.Mesh(new THREE.SphereGeometry(1.3,16,12), new THREE.MeshBasicMaterial({ color: '#ffe390' })); sun.position.set(-17,14,-25);scene.add(sun);
  const count=260, positions=new Float32Array(count*6); for(let i=0;i<count;i++){const x=(Math.random()-.5)*55,y=Math.random()*22-1,z=(Math.random()-.5)*40;positions.set([x,y,z,x-.13,y-.8,z],i*6)}
  const rainGeometry=new THREE.BufferGeometry(); rainGeometry.setAttribute('position',new THREE.BufferAttribute(positions,3));
  const rainMaterial=new THREE.LineBasicMaterial({color:0xc7e8ff,transparent:true,opacity:0,depthWrite:false});const rain=new THREE.LineSegments(rainGeometry,rainMaterial);scene.add(rain);
  const boltMat=new THREE.LineBasicMaterial({color:0xfff8cc,transparent:true,opacity:0});const boltGeo=new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(12,13,-15),new THREE.Vector3(10.8,10.8,-15),new THREE.Vector3(12,10.8,-15),new THREE.Vector3(9.9,7.6,-15)]);const bolt=new THREE.Line(boltGeo,boltMat);scene.add(bolt);
  const white=new THREE.Color('#fffdf0'), gray=new THREE.Color('#778893'), sunColor=new THREE.Color('#ffe390'), dimSun=new THREE.Color('#9aabb4');
  let desired=0, weather=0, last=performance.now(), nextFlash=0, flashUntil=0;
  window.addEventListener('obsidian-weather', event => { desired=THREE.MathUtils.clamp(Number(event.detail)||0,0,1); });
  function resize(){const w=canvas.clientWidth,h=canvas.clientHeight;if(!w||!h)return;renderer.setSize(w,h,false);camera.aspect=w/h;camera.fov=w<700?55:42;camera.updateProjectionMatrix()}
  window.addEventListener('resize',resize);resize();
  function frame(now){const dt=Math.min((now-last)/1000,.05);last=now;weather+=(desired-weather)*Math.min(1,dt*3);scene.background.copy(sunny).lerp(storm,weather);scene.fog.color.copy(scene.background);cloudMaterial.color.copy(white).lerp(gray,weather);ambient.intensity=2.2-weather*1.15;sunLight.intensity=2.2-weather*1.8;sun.material.color.copy(sunColor).lerp(dimSun,weather);rainMaterial.opacity=Math.max(0,(weather-.2)*.75);
    if(!reduced.matches){clouds.forEach((cloud,i)=>{cloud.position.x+=dt*(.08+i*.02);if(cloud.position.x>29)cloud.position.x=-29});if(weather>.2){for(let i=0;i<count;i++){const k=i*6;positions[k+1]-=dt*13;positions[k+4]-=dt*13;if(positions[k+1]<-1){positions[k+1]=20;positions[k+4]=19.2}}rainGeometry.attributes.position.needsUpdate=true}if(weather>.72 && now>nextFlash){flashUntil=now+100;nextFlash=now+3500+Math.random()*3500}const flash=now<flashUntil&&weather>.72;boltMat.opacity=flash?.85:0;if(flash)ambient.intensity+=.8}else boltMat.opacity=0;
    renderer.render(scene,camera);requestAnimationFrame(frame)}
  requestAnimationFrame(frame);
} catch (error) { console.warn('Cenário 3D indisponível:', error.message); }
