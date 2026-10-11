// Controlled additive Composite migration; FairyGUI XML/atlas/bytes remain helper-owned.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../../..');
const assets = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const recipePath = path.join(root, 'ArtSource/UIKits/MxUiFoundation/ui-component-recipes.json');
const resourcePath = path.join(root, 'ArtSource/UIKits/MxUiFoundation/ui-art-resources.json');
const backup = path.join(root, 'Library/UtilityCardImplementation', 'backup-' + crypto.randomUUID());
fs.mkdirSync(backup, { recursive:true });
for (const file of [recipePath, resourcePath]) fs.copyFileSync(file, path.join(backup, path.basename(file)));
const sourceDir = path.join(root, 'ArtSource/UIKits/MxUiFoundation/images/utility_card_v1');
const runtimeDir = path.join(root, 'Assets/Resources/MxFramework/UI/CardTheme/Utility');
fs.mkdirSync(sourceDir, {recursive:true}); fs.mkdirSync(runtimeDir, {recursive:true});
const resourceNames = {UtilityShell:'shell', UtilityGoldShell:'gold_shell', UtilityHolographicShell:'holographic_shell', UtilityMomentumRecovery:'momentum_recovery', UtilityPursuitMark:'pursuit_mark',
  UtilityReturnMark:'return_mark', UtilityDefensePreparation:'defense_preparation', UtilityHurtCharge:'hurt_charge'};
for (const [name,file] of Object.entries(assets)) {
  if (!resourceNames[name] || !fs.existsSync(file)) throw Error('Missing generated asset: '+name);
  fs.copyFileSync(file, path.join(sourceDir,name+'.png'));
  fs.copyFileSync(file, path.join(runtimeDir,name+'.png'));
}
function endOf(text,start) {
  let depth=0,quoted=false,escaped=false;
  for(let i=start;i<text.length;i++) {
    const c=text[i]; if(quoted){if(escaped)escaped=false;else if(c==='\\')escaped=true;else if(c==='"')quoted=false;continue;}
    if(c==='"'){quoted=true;continue;} if(c==='{'||c==='[')depth++;else if(c==='}'||c===']')if(--depth===0)return i+1;
  }
  throw Error('Unclosed authority JSON');
}
function upsert(text, arrayName, idName, value) {
  const at=text.indexOf('"'+idName+'": '+JSON.stringify(value[idName]));
  const formatted=JSON.stringify(value,null,2).replaceAll('\n','\n    ');
  if(at>=0){const start=text.lastIndexOf('{',at);return text.slice(0,start)+formatted+text.slice(endOf(text,start));}
  const start=text.indexOf('[',text.indexOf('"'+arrayName+'"')),end=endOf(text,start)-1;
  return text.slice(0,end).trimEnd()+',\n    '+formatted+'\n  '+text.slice(end);
}
let resources=fs.readFileSync(resourcePath,'utf8');
for(const [name,id] of Object.entries(resourceNames)) resources=upsert(resources,'resources','resourceId',{
  resourceId:'ui.card.utility.'+id, relativePath:'images/utility_card_v1/'+name+'.png', category:name.endsWith('Shell')?'PanelFrame':'Icon',
  displayName:name,description:'Approved functional-card presentation artwork; gameplay text and values are bound separately.',
  tags:['card','utility','imagegen','v1'],recommendedFairyGuiUsage:{policy:'Stretch',designSize:{width:378,height:528},scale9Grid:null,preserveAspect:false},
  replacement:{requireAlphaChannel:true,requireNonOpaqueAlpha:true,requireSamePixelSize:true,allowStretch:true,minPixelWidth:378,minPixelHeight:378,replaceTogether:false}});
const catalog=JSON.parse(fs.readFileSync(recipePath,'utf8'));
const recipe=catalog.recipes.find(r=>r.componentId==='ui.component.tactical.player_card_face');
if(!recipe)throw Error('Missing player card recipe');
const base=(name,type,x,y,width,height,visible=false)=>({nodeId:name,name,type,order:0,bindingSlot:'',parentContainerId:'',visible,
  layout:{x,y,width,height,horizontalAnchor:'Left',verticalAnchor:'Top'},graph:null,text:null,image:null,component:null,list:null});
const text=(name,x,y,w,h,size,align='center')=>({...base(name,'Text',x,y,w,h),text:{value:'',font:'MxUI Sans SC',fontSize:size,color:'#ff142b46',
  align,bold:false,singleLine:false,input:false,restrict:'',maxLength:0,verticalAlign:'middle'}});
const fields=[text('utilityType',86,68,262,22,16),text('utilityTrigger',30,317,318,30,21),text('utilityPrefix',48,353,92,76,26,'right'),
  text('utilityValue',142,341,74,100,62),text('utilitySuffix',222,353,142,76,26,'left'),text('utilityEffect',24,355,330,76,28),
  text('utilityDetails',26,418,326,44,18),text('utilityLifetime',26,468,326,24,17)];
fields.unshift({...base('assetUtilityShell','Image',0,0,378,528),image:{resourceId:'ui.card.utility.shell',tint:'#ffffffff'}});
const added=new Set([...fields.map(n=>n.name),...Object.keys(resourceNames).map(n=>'asset'+n)]);
const nodes=recipe.template.nodes.filter(n=>!added.has(n.name));
nodes.splice(nodes.findIndex(n=>n.name==='focus'),0,...fields);
nodes.forEach((n,i)=>n.order=i);recipe.template.nodes=nodes;
const recipes=upsert(fs.readFileSync(recipePath,'utf8'),'recipes','componentId',recipe);
JSON.parse(resources);JSON.parse(recipes);
fs.writeFileSync(resourcePath,resources,'utf8');fs.writeFileSync(recipePath,recipes,'utf8');
fs.writeFileSync(path.join(backup,'generated-assets.json'),JSON.stringify(assets,null,2));
console.log(JSON.stringify({backup,component:recipe.componentId,addedControls:fields.map(n=>n.name)}));
