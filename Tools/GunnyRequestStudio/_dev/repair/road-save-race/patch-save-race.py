import os, re, sys

BASE = r'C:\Gunny\Gunny12.6\Full Source'
P = os.path.join(BASE, r'game.server\Game.Server.GameObjects\GamePlayer.cs')
t = open(P, 'r', encoding='utf-8-sig', newline='').read()

def sub(pattern, repl, label):
    global t
    t2, n = re.subn(pattern, repl, t, count=1, flags=re.S)
    if n == 0:
        print('MISS:', label); return False
    t = t2
    print('OK  :', label)
    return True

# 1) Login: set m_loaded = true sau LoadFromDatabase()
ok = sub(r'(if \(LoadFromDatabase\(\)\)\s*\{\s*\n)(\s*)(Out\.SendLoginSuccess\(\);)',
         lambda m: m.group(1) + m.group(2) + 'm_loaded = true;\n' + m.group(2) + m.group(3),
         'set m_loaded in Login')
if not ok:
    # fallback: in xung quanh
    i = t.find('LoadFromDatabase()')
    print(repr(t[i-120:i+300]))

# 2) SaveIntoDatabase guard
sub(r'(public virtual bool SaveIntoDatabase\(\)\s*\{\s*try\s*\{\s*\n)(\s*)(m_character\.Money = LogMgr\.GetMoney)',
    lambda m: m.group(1) + m.group(2) + '// Bo qua player dang login (chua LoadFromDatabase xong) - tranh NullReferenceException\n'
              + m.group(2) + 'if (!m_loaded || m_character == null) return false;\n' + m.group(2) + m.group(3),
    'guard SaveIntoDatabase')

# 3) saveTeamSelfActive null-safe
sub(r'(public void saveTeamSelfActive\(\)\s*\{\s*\n)(\s*)(PlayerData playerData = new PlayerData\(\);)',
    lambda m: m.group(1) + m.group(2) + 'if (PlayerCharacter == null) return;\n'
              + m.group(2) + 'if (PlayerCharacter.TeamSelfActive == null)\n'
              + m.group(2) + '    PlayerCharacter.TeamSelfActive = new List<Sys_Users_TeamSelfActive>();\n'
              + m.group(2) + m.group(3),
    'null-safe saveTeamSelfActive')

# 4) saveTeamDayActive null-safe
sub(r'(public void saveTeamDayActive\(\)\s*\{\s*\n)(\s*)(PlayerData playerData = new PlayerData\(\);\s*\n\s*if \(PlayerCharacter\.TeamDayActive\.ID == 0\))',
    lambda m: m.group(1) + m.group(2) + 'if (PlayerCharacter == null) return;\n'
              + m.group(2) + 'if (PlayerCharacter.TeamDayActive == null)\n'
              + m.group(2) + '{\n'
              + m.group(2) + '    PlayerCharacter.TeamDayActive = new Sys_Users_TeamDayActive();\n'
              + m.group(2) + '    PlayerCharacter.TeamDayActive.UserID = PlayerId;\n'
              + m.group(2) + '}\n'
              + m.group(3),
    'null-safe saveTeamDayActive')

open(P, 'w', encoding='utf-8-sig', newline='').write(t)
print('GamePlayer saved')

# ---------------------------------------------------------------- GameServer.cs
P2 = os.path.join(BASE, r'game.server\Game.Server\GameServer.cs')
t2 = open(P2, 'r', encoding='utf-8-sig', newline='').read()
pat = re.compile(r'(\t+for \(int i = 0; i < allPlayers\.Length; i\+\+\)\s*\n\s*\{\s*\n)(\s*)(allPlayers\[i\]\.SaveIntoDatabase\(\);)')
# use original structure from source: foreach GamePlayer gamePlayer in array
pat = re.compile(r'(\s*)foreach \(GamePlayer gamePlayer in array\)\s*\n\s*\{\s*\n(\s*)(gamePlayer\.SaveIntoDatabase\(\);\s*\n\s*num\+\+;)')
m = pat.search(t2)
if not m:
    print('MISS SaveTimerProc')
else:
    indent = m.group(2)
    new = (m.group(1) + 'foreach (GamePlayer gamePlayer in array)\n'
           + indent + '{\n'
           + indent + '    // Bo qua player dang login (chua LoadFromDatabase xong) - tranh NRE\n'
           + indent + '    if (gamePlayer == null || !gamePlayer.Loaded) continue;\n'
           + indent + m.group(3))
    t2 = t2[:m.start()] + new + t2[m.end():]
    open(P2, 'w', encoding='utf-8-sig', newline='').write(t2)
    print('OK  : GameServer SaveTimerProc')
print('--- done')
