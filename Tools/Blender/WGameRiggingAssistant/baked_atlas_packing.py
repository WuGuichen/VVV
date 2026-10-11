"""Joint concave UV packing on an isolated proxy; source meshes stay untouched."""
import math
import numpy as np


def parts(record):
    return record.get('parts',[(record['object'],record['loops'],record['coords'])])


def source_density(record):
    ratios=[]
    for obj,loops,coords in parts(record):
        layer=obj.data.uv_layers.get('DetailUV') or obj.data.uv_layers.get('FaceDetailUV')
        if layer is None:continue
        source=np.array([layer.data[i].uv[:] for i in loops]).reshape(-1,3,2)
        triangles=coords.reshape(-1,3,2)
        a=np.linalg.norm(np.roll(source,-1,axis=1)-source,axis=2)
        b=np.linalg.norm(np.roll(triangles,-1,axis=1)-triangles,axis=2)
        valid=a>1e-10
        if np.any(valid):ratios.extend((b[valid]/a[valid]).tolist())
    return float(np.median(ratios)) if ratios else 1.


def pack_once(items, cells, size, pad, angle, order, rotation_method='CARDINAL', rotate_largest_only=False):
    """Native exact-shape packing, with the small palette cells pinned in place.

    Compatible overlap groups become one rigid island (merge_overlap). Separate
    records start far apart, so unrelated groups can never merge accidentally.
    Recover an affine transform and reject any group that the packer split.
    """
    import bpy
    import bmesh
    previous=bpy.context.view_layer.objects.active
    selected=list(bpy.context.selected_objects)
    mesh=None;obj=None
    try:
        vertices=[];faces=[];sources={};face_records=[]
        rotation=np.array(((math.cos(angle),-math.sin(angle)),(math.sin(angle),math.cos(angle))))
        ordered=sorted(items,key=lambda item:(-float(np.prod(np.ptp(item['points'],axis=0))) if order==0 else -float(np.max(np.ptp(item['points'],axis=0))),item['id']))
        largest=max(items,key=lambda item:float(np.prod(np.ptp(item['points'],axis=0))))['id']
        stride=max(4.,1.+max(float(np.linalg.norm(np.ptp(item['points'],axis=0))) for item in items))
        for ordinal,item in enumerate(ordered):
            points=item['points']@rotation.T if not rotate_largest_only or item['id']==largest else item['points'].copy()
            points-=points.min(axis=0)
            points+=np.array((4.+ordinal*stride,4.))
            lookup={}
            for triangle in points.reshape(-1,3,2):
                face=[]
                for point in triangle:
                    key=tuple(np.round(point,7))
                    if key not in lookup:
                        lookup[key]=len(vertices);vertices.append((*point,0.))
                    face.append(lookup[key])
                if len(set(face))==3:
                    faces.append(tuple(face));face_records.append(item['id'])
            sources[item['id']]=points
        # Every palette cell includes its own gutters; pin the whole occupied
        # rectangle, leaving the unused remainder of the row available.
        for x,y,pitch,_ in cells:
            start=len(vertices)
            vertices.extend([(x/size,y/size,0),((x+pitch)/size,y/size,0),((x+pitch)/size,(y+pitch)/size,0),(x/size,(y+pitch)/size,0)])
            faces.append(tuple(range(start,start+4)));face_records.append(None)
        mesh=bpy.data.meshes.new('WGameAtlasPackingProxy');mesh.from_pydata(vertices,[],faces);mesh.update()
        obj=bpy.data.objects.new('WGameAtlasPackingProxy',mesh);bpy.context.collection.objects.link(obj)
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        uv=mesh.uv_layers.new(name='PackUV')
        for loop in mesh.loops:uv.data[loop.index].uv=mesh.vertices[loop.vertex_index].co.xy
        tags=mesh.attributes.new(name='PackingChart',type='INT',domain='FACE')
        for item,tag in zip(tags.data,face_records):item.value=-1 if tag is None else tag
        if len(faces)>128:
            # The proxy is planar: dissolve interior tessellation before the
            # native contour search. UV boundaries and shared-overlap outlines
            # stay intact, while thousands of interior hair edges disappear.
            bm=bmesh.new()
            try:
                bm.from_mesh(mesh)
                bmesh.ops.dissolve_limit(bm,angle_limit=.001,use_dissolve_boundaries=False,
                    verts=list(bm.verts),edges=list(bm.edges),delimit={'UV'})
                bm.to_mesh(mesh);mesh.update()
            finally:bm.free()
        bpy.ops.object.mode_set(mode='EDIT')
        bm=bmesh.from_edit_mesh(mesh);layer=bm.loops.layers.uv.verify()
        tags=bm.faces.layers.int.get('PackingChart')
        for face in bm.faces:
            for loop in face.loops:loop[layer].pin_uv=face[tags]<0
        bmesh.update_edit_mesh(mesh)
        bpy.ops.uv.select_all(action='SELECT')
        # Blender's fractional margin is per island, not the gap between two.
        bpy.ops.uv.pack_islands(udim_source='ACTIVE_UDIM',rotate=True,rotate_method=rotation_method,scale=True,
            merge_overlap=True,margin_method='FRACTION',margin=(pad+.5)/size,
            pin=True,pin_method='LOCKED',shape_method='CONCAVE')
        bpy.ops.object.mode_set(mode='OBJECT');uv=mesh.uv_layers['PackUV']
        tags=mesh.attributes['PackingChart']
        originals={item['id']:[] for item in items};outputs={item['id']:[] for item in items}
        for face,tag in zip(mesh.polygons,tags.data):
            if tag.value<0:
                for loop in face.loop_indices:
                    if not np.allclose(uv.data[loop].uv[:],mesh.vertices[mesh.loops[loop].vertex_index].co.xy[:],atol=1e-7,rtol=0):return None
            else:
                for loop in face.loop_indices:
                    originals[tag.value].append(mesh.vertices[mesh.loops[loop].vertex_index].co.xy[:])
                    outputs[tag.value].append(uv.data[loop].uv[:])
        result={}
        for item in items:
            original=np.array(originals[item['id']]);packed=np.array(outputs[item['id']])
            if len(original)<3:return None
            matrix=np.linalg.lstsq(np.column_stack((original,np.ones(len(original)))),packed,rcond=None)[0]
            if np.max(abs(np.column_stack((original,np.ones(len(original))))@matrix-packed))>2e-5:
                return None
            # Fit directly back to the item's local coordinates, including the
            # seed rotation and translation, for exact shared-UV correspondence.
            seeded=sources[item['id']]
            target=np.column_stack((seeded,np.ones(len(seeded))))@matrix
            if not np.isfinite(target).all() or target.min()<-1e-6 or target.max()>1.000001:return None
            result[item['id']]=target
        return result
    finally:
        if obj and obj.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        if obj:bpy.data.objects.remove(obj,do_unlink=True)
        if mesh and mesh.users==0:bpy.data.meshes.remove(mesh)
        for item in selected:
            if item.name in bpy.context.view_layer.objects:item.select_set(True)
        bpy.context.view_layer.objects.active=previous


def validate_gutters(targets,cells,size,pad):
    """Independent pixel coverage check, including the bake dilation radius."""
    occupied=np.zeros((size,size),dtype=bool)
    for x,y,pitch,_ in cells:occupied[y:y+pitch,x:x+pitch]=True
    for coords in targets.values():
        points=coords*size
        low=np.maximum(0,np.floor(points.min(axis=0)).astype(int)-pad-1)
        high=np.minimum(size-1,np.ceil(points.max(axis=0)).astype(int)+pad+1)
        mask=np.zeros((high[1]-low[1]+1,high[0]-low[0]+1),dtype=bool)
        for triangle in points.reshape(-1,3,2):
            triangle=triangle-low
            lo=np.maximum(0,np.floor(triangle.min(axis=0)).astype(int));hi=np.minimum(np.array(mask.shape[::-1])-1,np.ceil(triangle.max(axis=0)).astype(int))
            if np.any(hi<lo):continue
            x,y=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
            e,f=triangle[1]-triangle[0],triangle[2]-triangle[0];det=e[0]*f[1]-e[1]*f[0]
            if abs(det)<1e-14:continue
            dx,dy=x-triangle[0,0],y-triangle[0,1]
            a=(dx*f[1]-dy*f[0])/det;b=(e[0]*dy-e[1]*dx)/det
            mask[lo[1]:hi[1]+1,lo[0]:hi[0]+1]|=(a>=-1e-6)&(b>=-1e-6)&(a+b<=1.000001)
        expanded=mask.copy();h,w=mask.shape
        for dy in range(-pad,pad+1):
            for dx in range(-pad,pad+1):
                if dx*dx+dy*dy>pad*pad:continue
                y0,y1=max(0,dy),min(h,h+dy);x0,x1=max(0,dx),min(w,w+dx)
                if y1>y0 and x1>x0:expanded[y0:y1,x0:x1]|=mask[y0-dy:y1-dy,x0-dx:x1-dx]
        region=occupied[low[1]:high[1]+1,low[0]:high[0]+1]
        if np.any(region&expanded):return False
        region|=expanded
    return True


def joint_pack(records,cells,size,pad):
    items=[];surfaces=[r for r in records if not r['detail'] and not r['constant']]
    total=max(1e-12,sum(r['area'] for r in surfaces))
    for record in records:
        if record['constant']:continue
        points=record['coords']-record['low']
        if record['detail']:
            points=points/max(source_density(record),1e-10)
        else:
            # A density budget, not a reserved band. Keep at least the previous
            # surface sampling footprint while letting it occupy any free gap.
            aspect=max(.08,min(12,float(record['span'][0]/record['span'][1])))
            area=max(64,size*size*.23*.65*record['area']/total)
            extent=np.maximum(4,np.ceil(np.array((math.sqrt(area*aspect),math.sqrt(area/aspect)))*.75))-1
            points=points/record['span']*extent/size
        items.append({'id':record['index'],'points':points,'detail':record['detail']})
    if not items:return {},{'detailScaleFromSource':1.,'detailScaleFromProbe':1.,'surfaceScale':1.,'packingCandidates':0}
    best=None;best_score=-1.;attempts=0
    # Quarter turns per island plus several non-cardinal seed orientations.
    # Very small fixtures don't need the extra search intended for full atlases.
    searches=[(0.,0,'CARDINAL',False)]
    if len(items)>8:
        # A tall dominant island can constrain the scale of the entire atlas.
        # Rotate it independently so other islands can fill its side pockets.
        searches.extend((math.radians(degrees),0,'CARDINAL',True) for degrees in (15,30,-15,-30))
        searches.append((0.,0,'ANY',False))
    else:searches.extend(((math.pi/4,0,'CARDINAL',False),(0.,1,'CARDINAL',False)))
    for angle,order,rotation_method,focused in searches:
        balance=1.04 if focused else 1.
        for iteration in range(2):
            inputs=[dict(item,points=item['points']*(1 if item['detail'] else balance)) for item in items]
            print('BAKE_CONTOUR_TRY',attempts+1,rotation_method,'angle',round(math.degrees(angle)),'focused',focused,flush=True)
            candidate=pack_once(inputs,cells,size,pad,angle,order,rotation_method,focused);attempts+=1
            if candidate is None:break
            ratios={}
            for item in items:
                a=item['points']-item['points'].mean(axis=0);b=candidate[item['id']]-candidate[item['id']].mean(axis=0)
                ratios[item['id']]=math.sqrt(float(np.sum(b*b)/max(1e-20,np.sum(a*a))))
            detail=min((ratios[i['id']] for i in items if i['detail']),default=1.)
            surface=min((ratios[i['id']] for i in items if not i['detail']),default=1.)
            score=detail if any(i['detail'] for i in items) else surface
            print('BAKE_CONTOUR_CANDIDATE',attempts,'detailScale',round(detail,4),'surfaceScale',round(surface,4),flush=True)
            if surface>=.999 and score>best_score and validate_gutters(candidate,cells,size,pad):
                best_score=score;best=(candidate,detail,surface)
                print('BAKE_CONTOUR_ACCEPT',attempts,round(score,4),flush=True)
            if not surfaces or not any(i['detail'] for i in items):break
            # Seek the largest detail scale without reducing the surface budget.
            balance*=1.015/max(.05,surface)
    if best is None:raise ValueError('No contour layout preserved rigid UV groups, surface density and texture gutters')
    candidate,detail,surface=best
    probe_ratios=[]
    for record in records:
        if record['constant'] or not record['detail']:continue
        a=record['coords']-record['coords'].mean(axis=0);b=candidate[record['index']]-candidate[record['index']].mean(axis=0)
        probe_ratios.append(math.sqrt(float(np.sum(b*b)/max(1e-20,np.sum(a*a)))))
    return candidate,{'detailScaleFromSource':round(detail,4),'detailScaleFromProbe':round(min(probe_ratios,default=1.),4),
        'surfaceScale':round(surface,4),'packingCandidates':attempts}
