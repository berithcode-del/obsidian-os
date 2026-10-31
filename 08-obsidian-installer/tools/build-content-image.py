#!/usr/bin/env python3
"""Build and verify a separate, non-bootable content card image on Linux/WSL."""
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import sys
import tempfile

ROOT = Path('/mnt/d/R36S-sistema-novo')
SOURCE = ROOT / 'Livros/Colecoes'
OUT = ROOT / '08-obsidian-installer/artifacts/releases/obsidian-conteudo-livros-1.0-lab.img'
SECTOR = 512
START = 2048
PART_SECTORS = 524288  # 256 MiB FAT32

def run(args, **kw):
    return subprocess.run(args, check=True, **kw)

def main():
    if OUT.exists():
        raise SystemExit('A imagem já existe; não será sobrescrita.')
    manifest = json.loads((SOURCE / 'manifesto.json').read_text(encoding='utf-8'))
    names = [Path(b['relative_path']).name for b in manifest['books']]
    if len(set(n.lower() for n in names)) != len(names):
        raise SystemExit('Nomes duplicados: não é possível criar a imagem sem substituições.')
    OUT.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='obsidian-content-') as tmp:
        partition = Path(tmp)/'content.fat'
        run(['mkfs.vfat','-F','32','-n','CONTEUDO','-C',str(partition),str(PART_SECTORS//2)],stdout=subprocess.DEVNULL)
        for folder in ['leitura','videos','roms','saves']:
            run(['mmd','-i',str(partition),'::/'+folder])
        entries=[]
        for b in manifest['books']:
            src=SOURCE/b['relative_path']
            name=src.name
            run(['mcopy','-i',str(partition),'-o',str(src),'::/leitura/'+name])
            expected=hashlib.sha256(src.read_bytes()).hexdigest()
            check=hashlib.sha256(run(['mtype','-i',str(partition),'::/leitura/'+name],stdout=subprocess.PIPE).stdout).hexdigest()
            if expected!=check: raise RuntimeError('Falha ao conferir '+name)
            entries.append({'file':'leitura/'+name,'sha256':expected,'size':src.stat().st_size})
        result=run(['fsck.vfat','-n',str(partition)],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
        mbr=bytearray(512)
        mbr[446:462]=struct.pack('<B3sB3sII',0,b'\xfe\xff\xff',0x0c,b'\xfe\xff\xff',START,PART_SECTORS)
        mbr[510:512]=b'\x55\xaa'
        try:
            with OUT.open('xb') as out, partition.open('rb') as source:
                out.write(mbr)
                out.seek(START*SECTOR)
                while block:=source.read(1024*1024): out.write(block)
                out.flush();os.fsync(out.fileno())
            h=hashlib.sha256()
            with OUT.open('rb') as image:
                for block in iter(lambda:image.read(1024*1024),b''):h.update(block)
            report={'type':'content-card-image','bootable':False,'system_included':False,
                    'target':'lab-only','image':OUT.name,'size':OUT.stat().st_size,
                    'sha256':h.hexdigest(),'sector_size':SECTOR,'partition_start':START,
                    'partition_sectors':PART_SECTORS,'filesystem':'FAT32','label':'CONTEUDO',
                    'book_count':len(entries),'empty_folders':['videos','roms','saves'],
                    'fsck':result.stdout,'entries':entries,
                    'note':'Conteúdo pessoal; não redistribuir. Não gravado nem testado no console.'}
            OUT.with_suffix('.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
            print(json.dumps({k:report[k] for k in ['image','size','sha256','book_count']},ensure_ascii=False))
        except Exception:
            if OUT.exists(): OUT.unlink()
            raise

if __name__=='__main__':main()
