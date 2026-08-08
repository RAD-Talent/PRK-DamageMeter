// PRK Damage Meter — native overlay for Project Rubi-Ka (AO 18.4)
// Reads the chat Log.txt the game writes (no injection, no automation).
// Build: run install.bat (uses the C# compiler included with Windows).
// A PRK community tool by Everkill (.everkill on Discord)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PRKDamageMeter
{
    public class Ev
    {
        public string Kind; public string Src; public string Dst; public long Amt; public long T; public bool Crit; public bool Glance; public string Via; public string DType; public string Special;
    }

    public class Fight
    {
        public long Start; public long End; public List<Ev> Events = new List<Ev>();
    }

    public class Row
    {
        public string Name; public long Total; public bool HasPets;
        public int Hits; public int Crits; public int Glances; public int Misses; public long Max;
        public long Weapon; public long Nano; public long Shield;
        public Dictionary<string, long> Types; public Dictionary<string, long[]> Specials;
    }

    // PRK-styled hover panel: one stat per line, bold values, section headers
    public class TipForm : Form
    {
        public List<string[]> L = new List<string[]>(); // [label, value, style] style: "h" header, "s" section, "" normal
        public TipForm()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = ColorTranslator.FromHtml("#0b1a20");
            DoubleBuffered = true;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x80; return cp; } // NOACTIVATE + TOOLWINDOW
        }
        int LineH(string style) { return style == "h" ? 22 : style == "s" ? 24 : 18; }
        public void SetLines(List<string[]> lines)
        {
            L = lines;
            using (Graphics g = CreateGraphics())
            using (Font fH = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Font fB = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (Font fN = new Font("Segoe UI", 9f))
            {
                float w = 230; int h = 10;
                foreach (string[] ln in L)
                {
                    Font lf = ln[2] == "h" ? fH : fN;
                    float lw = g.MeasureString(ln[0], lf).Width + g.MeasureString(ln[1], fB).Width + 46;
                    if (lw > w) w = lw;
                    h += LineH(ln[2]);
                }
                Width = (int)w + 12; Height = h + 8;
            }
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (Pen border = new Pen(ColorTranslator.FromHtml("#2a4a56")))
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            using (Font fH = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Font fB = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (Font fN = new Font("Segoe UI", 9f))
            using (Font fS = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (Brush txt = new SolidBrush(ColorTranslator.FromHtml("#dee8ea")))
            using (Brush dim = new SolidBrush(ColorTranslator.FromHtml("#7e9094")))
            using (Brush cyan = new SolidBrush(ColorTranslator.FromHtml("#5fd7e2")))
            using (Brush gold = new SolidBrush(ColorTranslator.FromHtml("#f2cc79")))
            {
                int y = 8;
                foreach (string[] ln in L)
                {
                    if (ln[2] == "h")
                    {
                        g.DrawString(ln[0], fH, cyan, 10, y);
                        SizeF vz = g.MeasureString(ln[1], fN);
                        g.DrawString(ln[1], fN, dim, Width - 10 - vz.Width, y + 2);
                    }
                    else if (ln[2] == "s")
                    {
                        g.DrawString(ln[0], fS, gold, 10, y + 8);
                        using (Pen p2 = new Pen(ColorTranslator.FromHtml("#1d3540")))
                            g.DrawLine(p2, 10, y + LineH("s") - 2, Width - 10, y + LineH("s") - 2);
                    }
                    else
                    {
                        g.DrawString(ln[0], fN, dim, 14, y);
                        if (ln[1].Length > 0)
                        {
                            SizeF vz = g.MeasureString(ln[1], fB);
                            g.DrawString(ln[1], fB, txt, Width - 10 - vz.Width, y);
                        }
                    }
                    y += LineH(ln[2]);
                }
            }
        }
    }

    public class MeterForm : Form
    {
        const int BASE_W = 330;
        const long GAP_MS = 10000;
        const int MAX_ROWS = 10;

        float S { get { return Width / (float)BASE_W; } }
        int HeaderH { get { return (int)(26 * S); } }
        int RowH { get { return (int)(24 * S); } }

        static string[] PROF_NAMES = { "Soldier","MartialArtist","Engineer","Fixer","Agent","Adventurer","Trader","Bureaucrat","Enforcer","Doctor","NanoTechnician","MetaPhysicist","Keeper","Shade" };
        static Dictionary<string, Color> PROF_COLOR = new Dictionary<string, Color> {
            {"Soldier", ColorTranslator.FromHtml("#c9484f")}, {"MartialArtist", ColorTranslator.FromHtml("#e0b23c")},
            {"Engineer", ColorTranslator.FromHtml("#c98f3c")}, {"Fixer", ColorTranslator.FromHtml("#5fd7e2")},
            {"Agent", ColorTranslator.FromHtml("#8fc55f")}, {"Adventurer", ColorTranslator.FromHtml("#4fae5c")},
            {"Trader", ColorTranslator.FromHtml("#cfae5f")}, {"Bureaucrat", ColorTranslator.FromHtml("#b0885f")},
            {"Enforcer", ColorTranslator.FromHtml("#d0685f")}, {"Doctor", ColorTranslator.FromHtml("#e05f8a")},
            {"NanoTechnician", ColorTranslator.FromHtml("#7f6fe0")}, {"MetaPhysicist", ColorTranslator.FromHtml("#a86fd0")},
            {"Keeper", ColorTranslator.FromHtml("#6fa8e0")}, {"Shade", ColorTranslator.FromHtml("#9a9ab0")},
            {"Unknown", ColorTranslator.FromHtml("#33505c")} };
        static Dictionary<string, string> PROF_ABBR = new Dictionary<string, string> {
            {"Soldier","SOL"},{"MartialArtist","MA"},{"Engineer","ENG"},{"Fixer","FIX"},{"Agent","AGT"},
            {"Adventurer","ADV"},{"Trader","TRA"},{"Bureaucrat","CRT"},{"Enforcer","ENF"},{"Doctor","DOC"},
            {"NanoTechnician","NT"},{"MetaPhysicist","MP"},{"Keeper","KPR"},{"Shade","SHD"},{"Unknown","?"} };
        // 16x16 profession icons extracted from the PRK client GUI (Graphics.uvga), embedded as PNG base64
        static Dictionary<string, string> PROF_ICON_B64 = new Dictionary<string, string> {
            {"Soldier","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAABIUlEQVR4nGNg+M9AELEZOSK4BFWzW/lIHv4r1L+TgAa3PUcgqiX2/5A88g+IoPbgUi1VP4vN0lti3zegUqAeDmtfnDa4bN4lP2EtG9DsPZ+BqsX3fQPag9MPGrM3R3z8J1ncKbz9I1C12O7P7BaeKGrQNMgf/+X/+p/vy38Sh/8JrHvB5+Cr0zMLpwb3dRvD3v8N+/Av4ds/z2f/FKp6bOYtVVtyCLsGrZkbgOpivvyL+fwv+vO/uK//Er/9dXvyF92TEIrbxjvsPUg1UGns139Rn/45LFkl6hvBhuxdZA2y+Y2Rn0CqE77+9b7xXD6/HmdUwlm2KzYlfP/nfucjgbhH5pgt3ih++B9XfC2xGoBI9NA/Dlt/EjRwh+aR4CRiEACpyi8vLNiMcQAAAABJRU5ErkJggg=="},
            {"MartialArtist","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAABb0lEQVR4nGNg+M9AGsIqKpRYyZdcL5BQSViDzIQNnnc/Rn36F/7hX8THf2HPP8pP2oBTg/zkTTFf/sV9+wck4Sj6yz/pzFosGmQnboz5/C8WqOHrv4TvYKWfQSj22z/pmpksUd3oGrye/Ir/BlLqduezwe67UZ//Jf4AoZAXn7mSu0Q2vUXRAPRf5Me/8d//OV58yRrXzxjWKVo02WjnNdNDd8UbV3DWbZc6+o89uAKhQbu6GeiG4OefORO7eBc/kzzyj2/eLa68STy5fTxtu8QP/JU8+o83tRnFBqDTNWdu5qzdLHX8H1AaaCRQGxCBGEB08C+TTzmKHwJe/1Ms7xJY/UQSphSOgEaIbPuJ7mmlBfsUW5dyTrkmfRxVNdC2I//YitZjiQf+golcxUskDv+TOvYPYg+QAdTAVooUd2gxzazryJq/SuLQPyCSPPxPeOMHtqJ1hNKSojxb2Vb26j0cVdtYDNyISnwkp1Y8CAAdiFNtNQqg5wAAAABJRU5ErkJggg=="},
            {"Engineer","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAA+UlEQVR4nGNg+M9AGsIU4rdwJEGDtKdP5Me/CjO2E6UBpPrtj/jv/8Lf/2PTtiCgQdrDN+rdj7hv/6I+/lPa9YVR2w2fBnYjx/AP/4BmR336q7T/j/D+f8JbvjPoeOOzQXHmjvC3v5X3/Qaqljz6T/r4P+7VX2RLJyv2LcfpBx4DS2Y9d8HN34GqRQ7+070MtPBf3Nd/cr0r8AUr0CU8y98CVcd8/pfw/V/Mp38iBVPxavjPIFIwJRao+gcwAP6qHPrLs/oro7oDPg1ANwBdAlStfOiP8L5/Qj1bCdgAREp9y2VKJ3Ot+yXcv41A0kBBavaE0xIBRHMNAI6KJ69bzzBlAAAAAElFTkSuQmCC"},
            {"Fixer","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAy0lEQVR4nGNg+M9AGiJTg0xGhVFdo1FDs1E9BgIK1jUCFaBoiPn8z/PZP/Nb/6xu/QGhOzDGrT9AQaAUUAGKhvivf9WqOxl+MWBFQCmgAlQbvvyDaBBd91hs5W3JAz+BCMgAciEagAqwa+Avn43sRRCXyhp4c/uQNYC4VLZBcPVjweW3RfaCPA1kALk4NHz+a9yKM1iBUkAFKBrCP/wDoqhP/6I//Yn++AcYTUAShD79AQpCZFE08MVV8Kc28qc3Y0epjUAFlCU+4hEAhg493auGePkAAAAASUVORK5CYII="},
            {"Agent","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAqElEQVR4nGNg+M9AGiKoQtLJjTQNmrM3k6DBbslqTktPYjUoRifIlbaT4AfjQw9I8LTz1n2CuV3EapAPDHG//5WEYI37+k8st5UoDSKmFq5bdjld/0BsxDltPwg0XquhhygNMllVkZ/+RX/5F/7hn3xBA2ENOqVV0Z//xXz+K5zXwWrkRFiD8fLdpusOa2+8gCdyECy54AjPp3/xKUXTIOMTwGkfSFADAHfCDu7sxuBrAAAAAElFTkSuQmCC"},
            {"Adventurer","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAsElEQVR4nGNg+M9AGsIqqp6Zh8YgoCHq0z+fM9eAqoEkURoCbj2HqAbqJEqD/Z4zEPdAGIQ1kOxpIHJctUEhMIRYDQ7bD8d8+QdEIgFxhDVAVMeCNQDZxDop7us/rZJqEvwQ/fmfQX0LURp0ympAGj791a2oAzIgJE4N0u7ecjN3Md5hTvj6V6eynl/fRGn+PsJOUpu2Lun7v+AX39n1bYnyA5drJDBR6K05RoKncSEAlWEZ/NrngLsAAAAASUVORK5CYII="},
            {"Trader","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAABL0lEQVR4nGNg+M9AGsIU4rPxkM2rN6yp1yqvF3bwIKBBftKGyI//oj79i/4MQjFf/jlffsFu7YtdA1A1UFHCt39Rn/+FPX0f+elf7Jd/Cd//Od/8iEUDp5VnxMd/8V//eT35IZdXDxQRsnU1Xroj9uu/uK//ZAsa0TXoNbQDHRD68juPvS+ytQZHX5gs38ll6oShoaoe6J7QJ++IDSWIBqDTidWgU9UI1BD85AOaCrm8Oik3TywaBBIqgYET/emv0tSNCEHngOB3oECTya3HEqzA4Ev8DgpKt2uvdCvrzZdtDX3/L+7bP2Docdt4Y9EAjCC3hz+B8ZD0AxT8QDL+27/wD/+A8YMzpjnN3YyXbjc79978/HsgCWQDnUogLQERq4EDm6EDkCQq8REbrEQiAA25OnCEB6VzAAAAAElFTkSuQmCC"},
            {"Bureaucrat","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAA2ElEQVR4nGNg+M9AGqK7BilXD6POCYYdUARkA0VwapD3D3J/+EuwfDrDLwYIArKBIkBxLBoUAoLcn/5jtw8Gstm0TZRT84AkkA0UAYoDZVE0iPlHe95+z2nnD2RzWnq4P/yZ8P0fkASyQSJ2/kBZoBqEBtmZe+wPX4V6I68x+vO/pB//gCSQDREEygLVoDhJeeFBz31HIU6X7lljcuoVkIRwgeJAWSyeVlp81GPvEbiPIQgoAhTHGawKs3abHXloevCu2YHbIPLIQ6AIgYgDqkBGA540qK8BANIKGechyRTsAAAAAElFTkSuQmCC"},
            {"Enforcer","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAk0lEQVR4nGNg+M9AGkLmyGRUGFTX61VBEZANFMGpga1CKPLTv+jPKAgoAhTHaUP4s/cxX/4hI6AIPieFf/gHQWHv/8HZ+DTwxVXwJdfzxpTzheeByOR6oAg+DVDPmGoppeYBScKhBEH6jR0xn/8BSWI1AAMUGD5AcmhrAEYZCRqA4QOMLxJCid3IARhlQJJYDfgRAA76IlBO99NqAAAAAElFTkSuQmCC"},
            {"Doctor","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAA10lEQVR4nGNg+M9AGkLjy2RUhDx5H/H8vdPZB0RpMKhtjP78D4giX30mSoNuRR1EQ9jT90RpMGxqh2p49gGfBqCLge4Gmhrx+htEQ/Snv0Bu+LP3QC/pz9+ErgHoYqg6bMhj10F0DTGvPyd8+5fw/V/sV4S6eKAIGHnuxtBgfOiB+fn3xmfeu93/Fvf1X8yXfxEf/pqde28OROffa87CcBIcqVW1QzQQ8DQc6VdBgxXoXeKCtbYR4m6gr4jSIJ1eAXQ0yD+HiEsabIYOEMSqZ0OUBsKIVA0ATNxN3YPrVWkAAAAASUVORK5CYII="},
            {"NanoTechnician","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAtklEQVR4nGNg+M9AGsIqyqplQZoGibg80jToLtzO7xxIgoaIj//UGycQq0EwtT7h2z/Hiy+J0iDs4ud881P8t38xn/+pTNuIUwNvUKbBzJUh7/7FfPkX9/Vf9GcQA6gNKGK5aodUYgELPNyQdYuk1pqu2h3+AaQaiPye/pAt7RT1DCbgB83FBxK+g8zmtvMjytNcJg7xX/8ZLN1FQrB63P8qk1ZGggbhjAbSYprd2pc0DfgQqRoAPawZgD7dY7sAAAAASUVORK5CYII="},
            {"MetaPhysicist","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAq0lEQVR4nGNg+M9AGqKaBp6gTNI0qGcXkaZBadomwhqEjUywamDRssBpg96UpRDVdje+QvRARHBq4LX18r//NubLv9iv/2I//wWycToJGCwaWQUamXlhzz4ANUR//gckgUg7K1cqsQDhKkxvWS9YAVQX9/Vf1Kd/QDbhUJJvng000uPeFyCpUDeZgAYhQ1MIQzm3AksQ4YkH7ew80iJOOqOCNA3oLiGoARcCABRcETEzfhP4AAAAAElFTkSuQmCC"},
            {"Keeper","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAy0lEQVR4nGNg+M9AGiJeqXJ8CgkalGPi5adtJVaDSv0EhUnriXWSx469BrtvEusHt41bPZ7+YzOwI6xBysPH88arqE//OG39CYeSjKdP9LsfMV/+yU/aQDhYFQvqY97/SPr+T2f5AQLxwO0S7rhxV9Tnf3Ff/7k9+IHidKwaJNMqgt78S/rxL+w90Ol+hGNawsHF9OJX/xe/9Jbvxxl0CKcHBnk+/8dm7CwYX47TeLgGsYAY35f/OOyDCEc8NBwnbORyiyKsmpikgYYAad4UAEopuxYAAAAASUVORK5CYII="},
            {"Shade","iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAA9UlEQVR4nGNg+M9AGsIlITdxA49jIAkaQt79kyloJlaDiKu/+9N/yrN3EaVBYdL6yI//Yr/+i//2z+3GGw5zNwIaYr78i/v6L/rzv4j3v31f/jNo7MCnQbesRnHRUfvzLxJ//HO6+pYvssCgrhmfBp3qJiCpXN6a9OOf6swtQLbDkev4NGhOWgokecycgl//4YksBrK5vRON2nqxa5Dy8BF3dIWw/W8+hYvLZ1YI2nti0cATlg9hSKeVez7+gSwlYG6PLx7Mt5wQL+jAKoVdg/3lt7hSAHZRDodgiyUbJAKjidUAQWKZDVJJRcrpBTxBmURpwIoAE3cW8BJdvkEAAAAASUVORK5CYII="},
        };
        Dictionary<string, Image> profIcons = new Dictionary<string, Image>();

        // ---- state ----
        string myName = "You";
        List<Ev> events = new List<Ev>();
        List<Fight> fights = new List<Fight>();
        Dictionary<string, string> tags = new Dictionary<string, string>();
        Dictionary<string, string> petOwner = new Dictionary<string, string>();
        HashSet<string> hidden = new HashSet<string>();
        string tab = "dmg";
        bool overallView = false;
        bool paused = false;
        string logPath = null;
        long lastPos = 0;
        string carry = "";
        Timer timer;
        List<Row> lastRows = new List<Row>();
        long lastDurMs = 1000;
        long lastGrand = 0;
        Point dragOff; bool dragging = false;
        string tipRow = null;
        TipForm tipForm;
        static HashSet<string> WEAPONSPECIALS = new HashSet<string> {
            "Burst","Fling Shot","Aimed Shot","Full Auto","Brawling","Brawl","Dimach","Fast Attack","Sneak Atck","Sneak Attack","Backstab" };
        // real elemental damage types; anything else in the "%s damage" slot is a special
        // attack name (Burst, Fling Shot...) or "unknown" = perk/proc damage
        static HashSet<string> ELEMENTS = new HashSet<string> {
            "melee","energy","projectile","fire","cold","poison","chemical","radiation","disease" };
        Dictionary<string, int[]> casts = new Dictionary<string, int[]>(); // nano -> [cast, landed, resisted]
        string lastCast = null;
        Dictionary<string, string> nanoProfs = new Dictionary<string, string>();
        long lastDumpTick = 0;
        bool autoHideMobs = true;
        bool showDpm = true;
        int scroll = 0;
        long lastGrowthTick = 0;
        List<Ev> xpEvents = new List<Ev>();
        List<string[]> xpLines = new List<string[]>();
        long appStart = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        long resetAt = 0;          // Reset cut-off: events older than this never count
        bool everLocked = false;   // true once we've parsed real events from the log we follow
        HashSet<string> knownActors = new HashSet<string>();
        static Regex PETNAME = new Regex("^(.+?)'s (.+)$");
        class Tmr { public string Name; public long End; public long Total; public bool Ready; }
        List<Tmr> timers = new List<Tmr>();
        Dictionary<string, long> nanoTimers = new Dictionary<string, long>(); // nano name -> auto-timer ms

        // ---- parser ----
        class Rule { public Regex Re; public Func<Match, Ev> Make; }
        List<Rule> rules = new List<Rule>();
        static Regex WRAP = new Regex("^\\[\"([^\"]*)\",\"([^\"]*)\",\"([^\"]*)\",(\\d+)\\](.*)$");

        Regex T(string template)
        {
            string r = Regex.Escape(template).Replace("%s", "(.+?)").Replace("%u", "(\\d+)").Replace("%d", "(\\d+)");
            return new Regex("^" + r + "$");
        }
        void AddRule(string template, Func<Match, Ev> make) { rules.Add(new Rule { Re = T(template), Make = make }); }

        void BuildRules()
        {
            AddRule("You hit %s with nanobots for %u points of %s damage.", m => Dmg(myName, m.Groups[1].Value, m.Groups[2].Value, "nano", m.Groups[3].Value, null));
            AddRule("You hit %s with %s for %u points of %s damage.", m => Dmg(myName, m.Groups[1].Value, m.Groups[3].Value, "weapon", m.Groups[4].Value, m.Groups[2].Value));
            AddRule("You hit %s for %u points of %s damage.", m => Dmg(myName, m.Groups[1].Value, m.Groups[2].Value, "weapon", m.Groups[3].Value, null));
            AddRule("You hit %s for %u points of damage.", m => Dmg(myName, m.Groups[1].Value, m.Groups[2].Value, "weapon"));
            AddRule("Your damage shield hit %s for %u points of damage.", m => Dmg(myName, m.Groups[1].Value, m.Groups[2].Value, "shield"));
            AddRule("Your reflect shield hit %s for %u points of damage.", m => Dmg(myName, m.Groups[1].Value, m.Groups[2].Value, "shield"));
            AddRule("Player %s hit you for %u points of %s damage.", m => Dmg(m.Groups[1].Value, myName, m.Groups[2].Value, "weapon", m.Groups[3].Value, null));
            AddRule("%s hit you for %u points of %s damage.", m => Dmg(m.Groups[1].Value, myName, m.Groups[2].Value, "weapon", m.Groups[3].Value, null));
            AddRule("You were attacked with nanobots from %s for %u points of %s damage.", m => Dmg(m.Groups[1].Value, myName, m.Groups[2].Value, "nano", m.Groups[3].Value, null));
            AddRule("You were attacked with nanobots for %u points of %s damage.", m => Dmg("Unknown", myName, m.Groups[1].Value, "nano", m.Groups[2].Value, null));
            AddRule("You were attacked with %s for %u points of %s damage.", m => Dmg("Unknown", myName, m.Groups[2].Value, "weapon", m.Groups[3].Value, null));
            AddRule("%s was attacked with nanobots from %s for %u points of %s damage.", m => Dmg(m.Groups[2].Value, m.Groups[1].Value, m.Groups[3].Value, "nano", m.Groups[4].Value, null));
            AddRule("%s was attacked with nanobots for %u points of %s damage.", m => Dmg("Unknown", m.Groups[1].Value, m.Groups[2].Value, "nano", m.Groups[3].Value, null));
            AddRule("%s was attacked with %s from %s for %u points of %s damage.", m => Dmg(m.Groups[3].Value, m.Groups[1].Value, m.Groups[4].Value, "weapon", m.Groups[5].Value, m.Groups[2].Value));
            AddRule("%s's damage shield hit %s for %u points of damage.", m => Dmg(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, "shield"));
            AddRule("%s's reflect shield hit %s for %u points of damage.", m => Dmg(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, "shield"));
            AddRule("You were hit for %u points of damage by %s's damage shield.", m => Dmg(m.Groups[2].Value, myName, m.Groups[1].Value, "shield"));
            AddRule("You were hit for %u points of damage by %s's reflect shield.", m => Dmg(m.Groups[2].Value, myName, m.Groups[1].Value, "shield"));
            AddRule("%s hit %s for %u points of %s damage.", m => Dmg(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, "weapon", m.Groups[4].Value, null));
            AddRule("You tried to hit %s, but missed!", m => Miss(myName, m.Groups[1].Value));
            AddRule("%s tried to hit you, but missed!", m => Miss(m.Groups[1].Value, myName));
            AddRule("%s tries to attack you with %s, but misses!", m => Miss(m.Groups[1].Value, myName));
            AddRule("%s parried your attack!", m => Miss(myName, m.Groups[1].Value));
            AddRule("You parried the attack from %s!", m => Miss(m.Groups[1].Value, myName));
            AddRule("You healed %s for %d points of health.", m => Heal(myName, m.Groups[1].Value, m.Groups[2].Value));
            AddRule("You got healed by %s for %d points of health.", m => Heal(m.Groups[1].Value, myName, m.Groups[2].Value));
            AddRule("You were healed for %u points.", m => Heal("Unknown", myName, m.Groups[1].Value));
            AddRule("Executing Nano Program: %s on item %s.", m => null);
            AddRule("Executing Nano Program: %s.", m => Cast("cast", m.Groups[1].Value));
            rules.Add(new Rule { Re = new Regex("^Nano program executed successfully\\.$"), Make = m => Cast("land", null) });
            rules.Add(new Rule { Re = new Regex("^Target resisted\\.$"), Make = m => Cast("resist", null) });
            rules.Add(new Rule { Re = new Regex("^Nano program aborted\\.$"), Make = m => Cast("int", null) });
            rules.Add(new Rule { Re = new Regex("^Nano execution aborted\\.$"), Make = m => Cast("int", null) });
            rules.Add(new Rule { Re = new Regex("^Nano execution aborted because either you or your target is teleporting\\.$"), Make = m => Cast("int", null) });
            rules.Add(new Rule { Re = new Regex("^You fumbled\\.$"), Make = m => Cast("int", null) });
            rules.Add(new Rule { Re = new Regex("^Your target countered the nano program\\.$"), Make = m => Cast("int", null) });
            AddRule("Your nano execution got interrupted by %s.", m => Cast("int", null));
            AddRule("%s executes %s within your NCU...", m => NcuBuff(m.Groups[1].Value, m.Groups[2].Value));
            AddRule("You received %u xp.", m => Xp("xp", m.Groups[1].Value));
            AddRule("You lost %u xp.", m => Xp("xploss", m.Groups[1].Value));
            AddRule("You gained %d points of Shadowknowledge.", m => Xp("sk", m.Groups[1].Value));
            AddRule("You gained %d points of Shadowknowledge as a side bonus.", m => Xp("sk", m.Groups[1].Value));
            AddRule("You lost %d points of Shadowknowledge.", m => Xp("skloss", m.Groups[1].Value));
            AddRule("You gained %d new Alien Experience Points.", m => Xp("axp", m.Groups[1].Value));
            // skill lock timers (trimmers, tradeskill debuffs, perk-style locks)
            rules.Add(new Rule
            {
                Re = new Regex("^Cannot use the \\[?(.+?)\\]? skill on this target for another ([0-9.]+) seconds?\\.$"),
                Make = m => TimerEv(m.Groups[1].Value, (long)(double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * 1000))
            });
            rules.Add(new Rule
            {
                // covers both: "Unable to perform action, X skill is locked, able in hh:mm:ss"
                //          and: "Unable to perform action X perk is locked, able in hh:mm:ss"
                Re = new Regex("^Unable to perform action,? (.+?) (?:skill|perk) is locked, able in (\\d+):(\\d+):(\\d+)\\.?$"),
                Make = m => TimerEv(m.Groups[1].Value,
                    (long.Parse(m.Groups[2].Value) * 3600 + long.Parse(m.Groups[3].Value) * 60 + long.Parse(m.Groups[4].Value)) * 1000)
            });
            rules.Add(new Rule
            {
                Re = new Regex("^(.+?) skill available\\.$"),
                Make = m => new Ev { Kind = "timerdone", Src = m.Groups[1].Value }
            });
        }
        Ev TimerEv(string skill, long ms) { return new Ev { Kind = "timer", Src = skill, Amt = ms }; }
        static Dictionary<string, string> SKILLPRETTY = new Dictionary<string, string> {
            {"mechanicalengineering","Mech Engineering"}, {"electricalengineering","Elec Engineering"},
            {"fieldquantumphysics","Quantum FT"}, {"weaponsmithing","Weapon Smithing"},
            {"nanoprogramming","Nano Programming"}, {"computerliteracy","Comp Literacy"},
            {"pharmatech","Pharma Tech"}, {"breakingentry","Breaking & Entry"},
            {"psychologicalmodification","Psycho Modi"}, {"biologicalmetamorphose","Bio Metamor"},
            {"materialmetamorphose","Matter Metam"}, {"materialcreation","Matter Crea"},
            {"sensoryimprovement","Sensory Impr"}, {"timeandspace","Time & Space"} };
        string PrettySkill(string raw)
        {
            string k = raw.Trim().Trim('[', ']');
            string v; if (SKILLPRETTY.TryGetValue(k.ToLowerInvariant().Replace(" ", ""), out v)) return v;
            return k.Length > 1 ? char.ToUpper(k[0]) + k.Substring(1) : k;
        }
        Ev Xp(string kind, string amt) { return new Ev { Kind = kind, Src = myName, Dst = myName, Amt = long.Parse(amt), Via = "xp" }; }
        Ev Cast(string what, string nano)
        {
            if (what == "cast" && nano != null)
            {
                lastCast = nano;
                if (!casts.ContainsKey(nano)) casts[nano] = new int[4];
                casts[nano][0]++;
                AutoProf(myName, nano);
            }
            else if (lastCast != null && casts.ContainsKey(lastCast))
            {
                if (what == "land")
                {
                    casts[lastCast][1]++;
                    long ntMs; // user-defined auto-timer for this nano (e.g. Shadowweb Spinner 6h)
                    if (nanoTimers.TryGetValue(lastCast, out ntMs)) StartTimer(lastCast, ntMs);
                }
                if (what == "resist") casts[lastCast][2]++;
                if (what == "int") casts[lastCast][3]++;
            }
            return null;
        }
        Ev NcuBuff(string caster, string nano) { AutoProf(caster, nano); return null; }
        void AutoProf(string who, string nano)
        {
            string p;
            if (who != null && !tags.ContainsKey(who) && nanoProfs.TryGetValue(nano, out p)) { tags[who] = p; SaveTags(); }
        }
        Ev Dmg(string src, string dst, string amt, string via) { return Dmg(src, dst, amt, via, null, null); }
        Ev Dmg(string src, string dst, string amt, string via, string dtype, string special) { return new Ev { Kind = "dmg", Src = src, Dst = dst, Amt = long.Parse(amt), Via = via, DType = dtype, Special = special }; }
        Ev Heal(string src, string dst, string amt) { return new Ev { Kind = "heal", Src = src, Dst = dst, Amt = long.Parse(amt), Via = "heal" }; }
        Ev Miss(string src, string dst) { return new Ev { Kind = "miss", Src = src, Dst = dst, Amt = 0 }; }

        Ev ParseLine(string line)
        {
            string msg = line; long t = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            Match w = WRAP.Match(line);
            if (w.Success) { msg = w.Groups[5].Value; t = long.Parse(w.Groups[4].Value) * 1000L; }
            msg = msg.Trim();
            bool crit = false, glance = false;
            if (msg.EndsWith("Critical hit!")) { crit = true; msg = Regex.Replace(msg, "\\s*Critical hit!$", ""); }
            if (msg.EndsWith("Glancing hit.")) { glance = true; msg = Regex.Replace(msg, "\\s*Glancing hit\\.$", ""); }
            foreach (Rule r in rules)
            {
                Match m = r.Re.Match(msg);
                if (m.Success) { Ev ev = r.Make(m); if (ev != null) { ev.T = t; ev.Crit = crit; ev.Glance = glance; } return ev; }
            }
            return null;
        }

        // ---- engine ----
        void AddEvent(Ev ev)
        {
            if (ev == null || paused) return;
            everLocked = true;               // real events parsed -> stay locked on this log
            if (ev.T < resetAt) return;      // never resurrect fights from before a Reset
            if (ev.Kind == "xp" || ev.Kind == "xploss" || ev.Kind == "sk" || ev.Kind == "skloss" || ev.Kind == "axp")
            {
                // xp session starts at app launch — ignore old lines replayed from the log
                if (ev.T >= appStart - 5000) xpEvents.Add(ev);
                return;
            }
            if (ev.Kind == "timer" || ev.Kind == "timerdone")
            {
                if (ev.T < appStart - 5000) return; // ignore old lines replayed from the log
                string nm = PrettySkill(ev.Src);
                Tmr tm = timers.FirstOrDefault(x => x.Name == nm);
                if (ev.Kind == "timer")
                {
                    if (tm == null) { tm = new Tmr { Name = nm, Total = ev.Amt }; timers.Add(tm); }
                    tm.End = ev.T + ev.Amt;
                    if (ev.Amt > tm.Total) tm.Total = ev.Amt;
                    tm.Ready = false;
                }
                else if (tm != null) { tm.Ready = true; tm.End = ev.T; }
                return;
            }
            // auto pet detection: "Dylan's robot" -> pet of Dylan (only if the owner is a known player)
            if (ev.Src != null)
            {
                if (!ev.Src.Contains(" ") && !ev.Src.Contains("'")) knownActors.Add(ev.Src);
                if (!petOwner.ContainsKey(ev.Src))
                {
                    Match pm = PETNAME.Match(ev.Src);
                    if (pm.Success && (pm.Groups[1].Value == myName || knownActors.Contains(pm.Groups[1].Value)))
                    { petOwner[ev.Src] = pm.Groups[1].Value; SaveTags(); }
                }
            }
            events.Add(ev);
            Fight f = fights.Count > 0 ? fights[fights.Count - 1] : null;
            bool gap = f == null || (ev.T - f.End) > GAP_MS;
            if (ev.Kind == "heal" && f != null)
            {
                // heals NEVER start a new fight - post-fight HoT ticks and top-off heals
                // attach to the previous fight without restarting the display. Only
                // in-combat heals (inside the gap) extend the fight's duration.
                f.Events.Add(ev);
                if (!gap && ev.T > f.End) f.End = ev.T;
                return;
            }
            if (gap) { f = new Fight { Start = ev.T, End = ev.T }; fights.Add(f); }
            f.Events.Add(ev);
            if (ev.T > f.End) f.End = ev.T;
        }
        string OwnerOf(string n) { string o; return petOwner.TryGetValue(n, out o) ? o : n; }
        bool IsFiltered(string who)
        {
            if (hidden.Contains(who)) return true;
            if (autoHideMobs && who.Contains(" ") && !petOwner.ContainsKey(who) && who != myName) return true;
            return false;
        }

        void AggregateXp()
        {
            lastRows = new List<Row>(); lastGrand = 0; lastDurMs = 1000;
            xpLines = new List<string[]>();
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long xpG = 0, xpL = 0, skG = 0, skL = 0, axpG = 0, bestXp = 0;
            int kills = 0, deaths = 0, skTicks = 0, axpTicks = 0;
            long firstT = 0, lastXpT = 0, xp5 = 0;
            long win = now - 300000;
            foreach (Ev e in xpEvents)
            {
                if (firstT == 0 || e.T < firstT) firstT = e.T;
                if (e.T > lastXpT && e.Kind != "xploss" && e.Kind != "skloss") lastXpT = e.T;
                if (e.Kind == "xp")
                {
                    xpG += e.Amt; kills++;
                    if (e.Amt > bestXp) bestXp = e.Amt;
                    if (e.T >= win) xp5 += e.Amt;
                }
                else if (e.Kind == "xploss") { xpL += e.Amt; deaths++; }
                else if (e.Kind == "sk") { skG += e.Amt; skTicks++; }
                else if (e.Kind == "skloss") skL += e.Amt;
                else if (e.Kind == "axp") { axpG += e.Amt; axpTicks++; }
            }
            if (xpEvents.Count == 0)
            {
                xpLines.Add(new string[] { "XP", "" });
                xpLines.Add(new string[] { "waiting for your first xp tick...", " " });
                return;
            }
            long elapsed = Math.Max(1000, now - firstT);
            double hours = elapsed / 3600000.0;
            long winMs = Math.Min(300000, elapsed);
            double paceH = xp5 / (winMs / 3600000.0);
            if (kills > 0 || xpL > 0)
            {
                xpLines.Add(new string[] { "XP", "" });
                xpLines.Add(new string[] { "gained", FmtN(xpG) });
                xpLines.Add(new string[] { "per hour", FmtN(xpG / hours) + "/h" });
                xpLines.Add(new string[] { "pace (last 5 min)", FmtN(paceH) + "/h" });
                xpLines.Add(new string[] { "xp ticks (kills)", kills + "  (" + FmtN(kills / hours) + "/h)" });
                xpLines.Add(new string[] { "avg per tick", FmtN(kills > 0 ? xpG / (double)kills : 0) });
                xpLines.Add(new string[] { "best single tick", FmtN(bestXp) });
                if (lastXpT > 0) xpLines.Add(new string[] { "last tick", FmtDur(Math.Max(0, now - lastXpT)) + " ago" });
                if (deaths > 0)
                {
                    xpLines.Add(new string[] { "lost (deaths)", "-" + FmtN(xpL) + "  (" + deaths + "x)" });
                    xpLines.Add(new string[] { "net xp", FmtN(xpG - xpL) });
                }
            }
            if (skTicks > 0 || skL > 0)
            {
                xpLines.Add(new string[] { "SHADOWKNOWLEDGE", "" });
                xpLines.Add(new string[] { "gained", FmtN(skG) });
                xpLines.Add(new string[] { "per hour", FmtN(skG / hours) + "/h" });
                xpLines.Add(new string[] { "ticks / avg", skTicks + " / " + FmtN(skTicks > 0 ? skG / (double)skTicks : 0) });
                if (skL > 0) xpLines.Add(new string[] { "lost", "-" + FmtN(skL) });
            }
            if (axpTicks > 0)
            {
                xpLines.Add(new string[] { "ALIEN XP", "" });
                xpLines.Add(new string[] { "gained", FmtN(axpG) });
                xpLines.Add(new string[] { "per hour", FmtN(axpG / hours) + "/h" });
                xpLines.Add(new string[] { "ticks / avg", axpTicks + " / " + FmtN(axpG / (double)axpTicks) });
            }
            xpLines.Add(new string[] { "SESSION", "" });
            xpLines.Add(new string[] { "tracking for", FmtDur(elapsed) });
            xpLines.Add(new string[] { "started", DateTimeOffset.FromUnixTimeMilliseconds(firstT).ToLocalTime().ToString("HH:mm") });
            lastDurMs = elapsed;
        }
        static string FmtDur(long ms)
        {
            long t = ms / 1000;
            if (t >= 3600) return (t / 3600) + "h " + ((t % 3600) / 60) + "m";
            if (t >= 60) return (t / 60) + "m " + (t % 60) + "s";
            return t + "s";
        }

        void Aggregate()
        {
            if (tab == "xp") { AggregateXp(); return; }
            if (tab == "casts")
            {
                lastRows = casts.Select(kv => new Row { Name = kv.Key, Total = kv.Value[0], Hits = kv.Value[1], Crits = kv.Value[2], Glances = kv.Value[3] })
                    .OrderByDescending(r => r.Total).ToList();
                lastGrand = 0; foreach (Row r in lastRows) lastGrand += r.Total;
                lastDurMs = 1000;
                return;
            }
            List<Ev> src; long dur;
            if (overallView || fights.Count == 0)
            {
                src = events; dur = 0;
                foreach (Fight f in fights) dur += Math.Max(1000, f.End - f.Start);
                if (dur == 0) dur = 1000;
            }
            else { Fight f = fights[fights.Count - 1]; src = f.Events; dur = Math.Max(1000, f.End - f.Start); }
            Dictionary<string, Row> agg = new Dictionary<string, Row>();
            HashSet<string> withPets = new HashSet<string>();
            foreach (Ev e in src)
            {
                if (e.Kind == "miss")
                {
                    string mwho = tab == "dmg" ? OwnerOf(e.Src) : tab == "taken" ? e.Dst : null;
                    if (mwho == null || IsFiltered(mwho)) continue;
                    Row mr; if (!agg.TryGetValue(mwho, out mr)) { mr = new Row { Name = mwho }; agg[mwho] = mr; }
                    mr.Misses++;
                    continue;
                }
                string who = null;
                if (tab == "dmg" && e.Kind == "dmg") who = OwnerOf(e.Src);
                else if (tab == "heal" && e.Kind == "heal") who = OwnerOf(e.Src);
                else if (tab == "taken" && e.Kind == "dmg") who = e.Dst;
                if (who == null || IsFiltered(who)) continue;
                Row r; if (!agg.TryGetValue(who, out r)) { r = new Row { Name = who }; agg[who] = r; }
                r.Total += e.Amt; r.Hits++;
                if (e.Crit) r.Crits++;
                if (e.Glance) r.Glances++;
                if (e.Amt > r.Max) r.Max = e.Amt;
                if (e.Via == "nano") r.Nano += e.Amt; else if (e.Via == "shield") r.Shield += e.Amt; else if (e.Via == "weapon") r.Weapon += e.Amt;
                if (e.DType != null)
                {
                    string dtl = e.DType.ToLowerInvariant();
                    if (ELEMENTS.Contains(dtl))
                    {
                        if (r.Types == null) r.Types = new Dictionary<string, long>();
                        long tv; r.Types.TryGetValue(e.DType, out tv); r.Types[e.DType] = tv + e.Amt;
                    }
                    else if (e.Special == null)
                    {
                        // "points of Burst damage" = special; "points of unknown damage" = perk/proc
                        string key = dtl == "unknown" ? "perk damage" : e.DType;
                        if (r.Specials == null) r.Specials = new Dictionary<string, long[]>();
                        long[] sp2; if (!r.Specials.TryGetValue(key, out sp2)) { sp2 = new long[2]; r.Specials[key] = sp2; }
                        sp2[0]++; sp2[1] += e.Amt;
                    }
                }
                if (e.Special != null)
                {
                    if (r.Specials == null) r.Specials = new Dictionary<string, long[]>();
                    long[] sp; if (!r.Specials.TryGetValue(e.Special, out sp)) { sp = new long[2]; r.Specials[e.Special] = sp; }
                    sp[0]++; sp[1] += e.Amt;
                }
                if (tab != "taken" && petOwner.ContainsKey(e.Src)) withPets.Add(who);
            }
            lastRows = agg.Values.OrderByDescending(r => r.Total).ToList();
            foreach (Row r in lastRows) r.HasPets = withPets.Contains(r.Name);
            lastGrand = agg.Values.Sum(r => r.Total);
            lastDurMs = dur;
        }

        // ---- log tail ----
        string FindLog()
        {
            try
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Funcom");
                if (!Directory.Exists(root)) return null;
                return Directory.GetFiles(root, "Log.txt", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            }
            catch { return null; }
        }
        void Poll()
        {
            // self-heal: while we've never parsed a single event, keep re-scanning for
            // the right Log.txt (handles latching onto a stale/empty log at startup).
            // Once real events are flowing we LOCK ON and never auto-switch - a combat
            // log is naturally silent between fights, and jumping to a chattier log
            // (e.g. a general chat window) would wipe the meter's data.
            if (!everLocked && events.Count == 0 && casts.Count == 0 && xpEvents.Count == 0
                && Environment.TickCount - lastGrowthTick > 15000)
            {
                lastGrowthTick = Environment.TickCount;
                string best = FindLog();
                if (best != null && !best.Equals(logPath, StringComparison.OrdinalIgnoreCase))
                {
                    logPath = best; lastPos = 0; carry = "";
                }
            }
            if (logPath == null || !File.Exists(logPath)) return;
            try
            {
                using (FileStream fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length < lastPos) { lastPos = 0; carry = ""; events.Clear(); fights.Clear(); }
                    if (fs.Length == lastPos) return;
                    lastGrowthTick = Environment.TickCount;
                    fs.Seek(lastPos, SeekOrigin.Begin);
                    byte[] buf = new byte[fs.Length - lastPos];
                    fs.Read(buf, 0, buf.Length);
                    lastPos = fs.Length;
                    string text = carry + Encoding.UTF8.GetString(buf);
                    string[] lines = text.Split('\n');
                    carry = lines[lines.Length - 1];
                    for (int i = 0; i < lines.Length - 1; i++) AddEvent(ParseLine(lines[i].TrimEnd('\r')));
                    Aggregate(); RecalcHeight(); Invalidate();
                    if (Environment.TickCount - lastDumpTick > 5000) { lastDumpTick = Environment.TickCount; WriteDumpScript(); }
                }
            }
            catch { }
        }

        // ---- in-game chat dump scripts (/prkdmg /prkheal /prkcast) ----
        static string HELP_FOOTER = "<br><font color='#7e9094'>Commands: /prkdmg damage | /prkheal healing | /prkcast nano casts<br>~ PRK Damage Meter by Everkill (.everkill on Discord)<br>Get it: <font color='#5fd7e2'>github.com/RAD-Talent/PRK-DamageMeter</font></font>";
        // In-game chat messages cap around 1024 chars. Instead of truncating raids,
        // pack rows into as many clickable links as needed - the script file gets one
        // line per part, and the game sends each line as its own chat message:
        // "PRK Damage (1/2)" "PRK Damage (2/2)".
        void WriteScriptParts(string scriptsDir, string fileName, string label, string title, List<string> rows, string emptyMsg)
        {
            const int LIMIT = 1000; // stay safely under the 1024 message cap
            string head = "<a href=\"text://<font color='#5fd7e2'>PRK Damage Meter</font><br>" + title + "<br><br>";
            if (rows.Count == 0) rows = new List<string> { emptyMsg };
            List<List<string>> parts = new List<List<string>>();
            List<string> cur = new List<string>(); int curLen = 0;
            foreach (string r in rows)
            {
                if (cur.Count > 0 && head.Length + curLen + r.Length + HELP_FOOTER.Length + label.Length + 48 > LIMIT)
                { parts.Add(cur); cur = new List<string>(); curLen = 0; }
                cur.Add(r); curLen += r.Length + 4;
            }
            if (cur.Count > 0) parts.Add(cur);
            StringBuilder f = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                string lbl = label + (parts.Count > 1 ? " (" + (i + 1) + "/" + parts.Count + ")" : "");
                string foot = i == parts.Count - 1 ? HELP_FOOTER : "";
                f.Append(head + string.Join("<br>", parts[i].ToArray()) + foot + "\">" + lbl + "</a>\n");
            }
            File.WriteAllText(Path.Combine(scriptsDir, fileName), f.ToString(), Encoding.GetEncoding(1252));
        }
        void WriteDumpScript()
        {
            try
            {
                if (logPath == null) return;
                int ix = logPath.IndexOf("\\Prefs\\");
                if (ix < 0) return;
                string scripts = Path.Combine(logPath.Substring(0, ix), "scripts");
                Directory.CreateDirectory(scripts);
                if (fights.Count == 0) return;
                List<Ev> pool; long dur;
                string scope;
                if (overallView)
                {
                    pool = events; dur = 0;
                    foreach (Fight ff in fights) dur += Math.Max(1000, ff.End - ff.Start);
                    if (dur == 0) dur = 1000;
                    scope = "overall";
                }
                else
                {
                    Fight f = fights[fights.Count - 1];
                    pool = f.Events; dur = Math.Max(1000, f.End - f.Start);
                    scope = "last fight";
                }
                Dictionary<string, Row> dmg = new Dictionary<string, Row>();
                Dictionary<string, Row> heal = new Dictionary<string, Row>();
                foreach (Ev e in pool)
                {
                    if (e.Kind == "miss")
                    {
                        string mw = OwnerOf(e.Src);
                        if (IsFiltered(mw)) continue;
                        Row mr; if (!dmg.TryGetValue(mw, out mr)) { mr = new Row { Name = mw }; dmg[mw] = mr; }
                        mr.Misses++;
                        continue;
                    }
                    Dictionary<string, Row> tgt = e.Kind == "dmg" ? dmg : e.Kind == "heal" ? heal : null;
                    if (tgt == null) continue;
                    string who = OwnerOf(e.Src);
                    if (IsFiltered(who)) continue;
                    Row r; if (!tgt.TryGetValue(who, out r)) { r = new Row { Name = who }; tgt[who] = r; }
                    r.Total += e.Amt; r.Hits++; if (e.Crit) r.Crits++; if (e.Amt > r.Max) r.Max = e.Amt;
                }
                // damage window (all players, split into parts as needed)
                WriteScriptParts(scripts, "prkdmg", "PRK Damage - " + scope + " " + (dur / 1000) + "s",
                    "<font color='#f2cc79'>Damage - " + scope + " (" + (dur / 1000) + "s)</font>",
                    RankLines(dmg, dur, true), "no damage recorded");
                // healing window
                WriteScriptParts(scripts, "prkheal", "PRK Healing - " + scope,
                    "<font color='#f2cc79'>Healing - " + scope + " (" + (dur / 1000) + "s)</font>",
                    RankLines(heal, dur, false), "no healing recorded");
                // casts window (session totals, aggregated per nano)
                List<string> cl = new List<string>();
                foreach (KeyValuePair<string, int[]> kv in casts.OrderByDescending(k => k.Value[0]))
                    cl.Add("<font color='#5fd7e2'>" + kv.Key + "</font> x" + kv.Value[0] + "  (" + kv.Value[1] + " landed, " + kv.Value[2] + " resisted" + (kv.Value[3] > 0 ? ", " + kv.Value[3] + " interrupted" : "") + ")");
                WriteScriptParts(scripts, "prkcast", "PRK Nano Casts - " + myName,
                    "<font color='#f2cc79'>" + myName + "'s nano casts (session)</font>", cl, "no casts recorded");
            }
            catch { }
        }
        List<string> RankLines(Dictionary<string, Row> agg, long dur, bool showProf)
        {
            List<Row> rows = agg.Values.OrderByDescending(r => r.Total).ToList();
            long grand = 0; foreach (Row r in rows) grand += r.Total;
            List<string> outl = new List<string>();
            if (rows.Count == 0) return outl;
            outl.Add("<font color='#f2cc79'>Total:</font> " + FmtN(grand));
            int rank = 1;
            foreach (Row r in rows)
            {
                string prof; if (!tags.TryGetValue(r.Name, out prof)) prof = null;
                double dps = r.Total / (dur / 1000.0);
                double pct = grand > 0 ? 100.0 * r.Total / grand : 0;
                outl.Add(rank + ". <font color='#5fd7e2'>" + r.Name + "</font>" + (showProf && prof != null ? " <font color='#e05f8a'>(" + prof + ")</font>" : "")
                    + " - " + FmtN(r.Total) + " (" + FmtRate(dps) + ", " + pct.ToString("0.0") + "%), " + r.Hits + " hits, " + r.Crits + " crits"
                    + (r.Misses > 0 ? ", " + r.Misses + " misses (hit " + (100.0 * r.Hits / Math.Max(1, r.Hits + r.Misses)).ToString("0") + "%)" : "")
                    + ", max " + FmtN(r.Max));
                rank++;
            }
            return outl;
        }

        // ---- persistence ----
        string TagFile { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PRK-DamageMeter.tags.txt"); } }
        string LegacyTagFile { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PRK-DamageMeter.tags.txt"); } }
        void LoadTags()
        {
            try
            {
                string tf = File.Exists(TagFile) ? TagFile : (File.Exists(LegacyTagFile) ? LegacyTagFile : null);
                if (tf == null) return;
                foreach (string ln in File.ReadAllLines(tf))
                {
                    string[] p = ln.Split('|');
                    if (p.Length >= 2 && p[0] == "hide") hidden.Add(p[1]);
                    if (p.Length >= 2 && p[0] == "me") myName = p[1];
                    if (p.Length >= 2 && p[0] == "automob") autoHideMobs = p[1] == "1";
                    if (p.Length >= 2 && p[0] == "dpm") showDpm = p[1] == "1";
                    if (p.Length >= 2 && p[0] == "width") { int w; if (int.TryParse(p[1], out w) && w >= 280 && w <= 700) Width = w; }
                    if (p.Length >= 3 && p[0] == "pos")
                    {
                        int px, py;
                        if (int.TryParse(p[1], out px) && int.TryParse(p[2], out py))
                        {
                            // only restore if the point is still on a connected screen
                            Point pt = new Point(px, py);
                            foreach (Screen sc in Screen.AllScreens)
                                if (sc.WorkingArea.Contains(new Rectangle(pt, new Size(50, 50)))) { Location = pt; break; }
                        }
                    }
                    if (p.Length >= 3 && p[0] == "prof") tags[p[1]] = p[2];
                    if (p.Length >= 3 && p[0] == "pet") petOwner[p[1]] = p[2];
                    if (p.Length >= 3 && p[0] == "ntimer") { long ms; if (long.TryParse(p[2], out ms) && ms > 0) nanoTimers[p[1]] = ms; }
                    if (p.Length >= 4 && p[0] == "timer")
                    {
                        long end, tot;
                        if (long.TryParse(p[2], out end) && long.TryParse(p[3], out tot)
                            && end > DateTimeOffset.Now.ToUnixTimeMilliseconds())
                            timers.Add(new Tmr { Name = p[1], End = end, Total = tot });
                    }
                }
            }
            catch { }
        }
        void SaveTags()
        {
            try
            {
                List<string> outl = new List<string>();
                outl.Add("me|" + myName);
                outl.Add("automob|" + (autoHideMobs ? "1" : "0"));
                outl.Add("dpm|" + (showDpm ? "1" : "0"));
                outl.Add("width|" + Width);
                outl.Add("pos|" + Location.X + "|" + Location.Y);
                foreach (KeyValuePair<string, string> kv in tags) outl.Add("prof|" + kv.Key + "|" + kv.Value);
                foreach (KeyValuePair<string, string> kv in petOwner) outl.Add("pet|" + kv.Key + "|" + kv.Value);
                foreach (string h in hidden) outl.Add("hide|" + h);
                foreach (KeyValuePair<string, long> kv in nanoTimers) outl.Add("ntimer|" + kv.Key + "|" + kv.Value);
                long nowMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                foreach (Tmr tm in timers) if (!tm.Ready && tm.End > nowMs) outl.Add("timer|" + tm.Name + "|" + tm.End + "|" + tm.Total);
                File.WriteAllLines(TagFile, outl.ToArray());
            }
            catch { }
        }


        // ---- one-click in-game chat window setup ----
        string ChatWindowXml(string windowName)
        {
            string[] ids = { "1073741825","1107296280","1107296276","1107296261","1107296265","1107296267","1107296263","1107296268","1107296275","1107296277","1107296259","1107296260","1107296262","1107296264","1107296266","1107296257","1107296274","1107296278","1107296258","1107296273","1107296279" };
            string[] names = { "System","Me Cast Nano","You gave health","You hit other with nano","Your pet hit by other","Me got XP","Me hit by player","Me got SK","Other misses","Me got health","Your pet hit by nano","Other hit by nano","Me hit by monster","You hit other","Other hit by other","Me hit by environment","Your misses","Me got nano","Me hit by nano","Your pet hit by monster","You gave nano" };
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<Archive code=\"0\">");
            sb.AppendLine("    <Array name=\"selected_group_ids\">");
            foreach (string id in ids) sb.AppendLine("        <Int64 value=\"" + id + "\" />");
            sb.AppendLine("    </Array>");
            sb.AppendLine("    <Array name=\"selected_group_names\">");
            foreach (string n in names) sb.AppendLine("        <String value='&quot;" + n + "&quot;' />");
            sb.AppendLine("    </Array>");
            sb.AppendLine("    <Archive code=\"0\" name=\"log_window_config\">");
            sb.AppendLine("        <Rect name=\"WindowFrame\" value=\"Rect(400.000000,300.000000,900.000000,470.000000)\" />");
            sb.AppendLine("        <Bool name=\"WindowPinButtonState\" value=\"false\" />");
            sb.AppendLine("    </Archive>");
            sb.AppendLine("    <Archive code=\"0\" name=\"chat_window_config\">");
            sb.AppendLine("        <Bool name=\"WindowPinButtonState\" value=\"false\" />");
            sb.AppendLine("        <Rect name=\"WindowFrame\" value=\"Rect(300.000000,600.000000,393.000000,600.000000)\" />");
            sb.AppendLine("        <Bool name=\"is_backmost\" value=\"false\" />");
            sb.AppendLine("        <Bool name=\"is_frontmost\" value=\"false\" />");
            sb.AppendLine("    </Archive>");
            sb.AppendLine("    <Archive code=\"0\" name=\"chat_view_config\" />");
            sb.AppendLine("    <Int32 name=\"visual_mode\" value=\"0\" />");
            sb.AppendLine("    <String name=\"output_group\" value='&quot;&quot;' />");
            sb.AppendLine("    <Float name=\"window_transparency_inactive\" value=\"0.300000\" />");
            sb.AppendLine("    <Float name=\"window_transparency_active\" value=\"0.800000\" />");
            sb.AppendLine("    <Bool name=\"show_timestamps\" value=\"false\" />");
            sb.AppendLine("    <Bool name=\"hide_input_when_inactive\" value=\"false\" />");
            sb.AppendLine("    <Bool name=\"deactivate_on_send\" value=\"true\" />");
            sb.AppendLine("    <Bool name=\"is_textinput_enabled\" value=\"true\" />");
            sb.AppendLine("    <Bool name=\"is_clickthrough\" value=\"false\" />");
            sb.AppendLine("    <Bool name=\"is_logged\" value=\"true\" />");
            sb.AppendLine("    <Bool name=\"is_message_fading_enabled\" value=\"false\" />");
            sb.AppendLine("    <Bool name=\"is_autosubscribe_window\" value=\"false\" />");
            sb.AppendLine("    <Bool name=\"is_window_open\" value=\"true\" />");
            sb.AppendLine("    <Int32 name=\"tab_index\" value=\"0\" />");
            sb.AppendLine("    <String name=\"window_name\" value='&quot;" + windowName + "&quot;' />");
            sb.AppendLine("    <Bool name=\"is_default_window\" value=\"false\" />");
            sb.AppendLine("    <Bool name=\"is_startup_window\" value=\"false\" />");
            sb.AppendLine("    <String name=\"name\" value='&quot;Damage&quot;' />");
            sb.AppendLine("</Archive>");
            return sb.ToString();
        }

        bool HasDamageWindow(string chatWindowsDir)
        {
            try
            {
                if (!Directory.Exists(chatWindowsDir)) return false;
                foreach (string wd in Directory.GetDirectories(chatWindowsDir))
                {
                    string cfg = Path.Combine(wd, "Config.xml");
                    if (File.Exists(cfg) && File.ReadAllText(cfg).Contains("&quot;Damage&quot;")) return true;
                }
            }
            catch { }
            return false;
        }

        List<string> FindCharDirs()
        {
            List<string> outl = new List<string>();
            try
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Funcom");
                if (!Directory.Exists(root)) return outl;
                foreach (string d in Directory.GetDirectories(root, "Char*", SearchOption.AllDirectories))
                    if (d.Contains("Prefs")) outl.Add(d);
            }
            catch { }
            return outl;
        }

        void SetupGameWindows(bool silentIfDone)
        {
            List<string> chars = FindCharDirs();
            if (chars.Count == 0)
            {
                if (!silentIfDone) MessageBox.Show(this, "No Anarchy Online character folders found under AppData\\Local\\Funcom.\nLog in to the game once, then try again.", "PRK Damage Meter");
                return;
            }
            bool gameRunning = System.Diagnostics.Process.GetProcessesByName("AnarchyOnline").Length > 0
                || System.Diagnostics.Process.GetProcessesByName("Anarchy").Length > 0;
            int created = 0, had = 0;
            List<string> pending = new List<string>();
            foreach (string cd in chars)
            {
                string cw = Path.Combine(Path.Combine(cd, "Chat"), "Windows");
                if (HasDamageWindow(cw)) { had++; continue; }
                pending.Add(cd);
            }
            if (pending.Count == 0)
            {
                if (!silentIfDone) MessageBox.Show(this, "All " + had + " characters already have the Damage window. You're set!", "PRK Damage Meter");
                return;
            }
            string warn = gameRunning ? "\n\nWARNING: the game looks like it's RUNNING. Log out fully first, or the game may overwrite the new window on exit." : "";
            DialogResult ok = MessageBox.Show(this,
                "Create the logged \"Damage\" chat window for " + pending.Count + " character(s)?\n" +
                "It will appear in game at next login, pre-set with all combat channels and logging enabled." + warn,
                "PRK Damage Meter - one-time game setup", MessageBoxButtons.YesNo);
            if (ok != DialogResult.Yes) return;
            foreach (string cd in pending)
            {
                try
                {
                    string cw = Path.Combine(Path.Combine(cd, "Chat"), "Windows");
                    Directory.CreateDirectory(cw);
                    int n = 1;
                    while (Directory.Exists(Path.Combine(cw, "Window" + n))) n++;
                    string wd = Path.Combine(cw, "Window" + n);
                    Directory.CreateDirectory(wd);
                    File.WriteAllText(Path.Combine(wd, "Config.xml"), ChatWindowXml("Window" + n));
                    created++;
                }
                catch { }
            }
            MessageBox.Show(this, "Done - Damage window created for " + created + " character(s).\nLog in and the window (and its log) start automatically.", "PRK Damage Meter");
        }

        // ---- form ----
        public MeterForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(60, 60);
            BackColor = ColorTranslator.FromHtml("#0a1519");
            Opacity = 0.95;
            Width = BASE_W;
            MinimumSize = new Size(280, 60);
            MaximumSize = new Size(700, 2000);
            DoubleBuffered = true;
            Text = "PRK Damage Meter";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            BuildRules(); LoadTags();
            foreach (KeyValuePair<string, string> kv in PROF_ICON_B64)
            {
                try
                {
                    Bitmap raw = new Bitmap(new MemoryStream(Convert.FromBase64String(kv.Value)));
                    Bitmap keyed = new Bitmap(raw);
                    keyed.MakeTransparent(keyed.GetPixel(0, 0)); // strip the green chroma key
                    profIcons[kv.Key] = keyed;
                }
                catch { }
            }
            try
            {
                string np = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nanoprofs.txt");
                if (File.Exists(np))
                    foreach (string ln in File.ReadAllLines(np))
                    { int ix = ln.LastIndexOf('|'); if (ix > 0) nanoProfs[ln.Substring(0, ix)] = ln.Substring(ix + 1); }
            }
            catch { }
            logPath = FindLog();
            RecalcHeight();
            Shown += delegate
            {
                List<string> chars = FindCharDirs();
                bool anyMissing = false;
                foreach (string cd in chars)
                    if (!HasDamageWindow(Path.Combine(Path.Combine(cd, "Chat"), "Windows"))) { anyMissing = true; break; }
                if (anyMissing) SetupGameWindows(true);
            };
            timer = new Timer(); timer.Interval = 1000;
            timer.Tick += delegate
            {
                Poll();
                if (timers.Count > 0)
                {
                    long nowMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    timers.RemoveAll(t2 => (t2.Ready || t2.End <= nowMs) && nowMs - t2.End > 8000); // READY shows ~8s then clears
                    RecalcHeight(); Invalidate();
                }
                if (tab == "xp") { Aggregate(); RecalcHeight(); Invalidate(); } // live rates/timers tick every second
            };
            timer.Start();
            MouseDown += OnDown; MouseMove += OnMove;
            MouseUp += delegate { if (dragging) { dragging = false; SaveTags(); } };
            MouseLeave += delegate { tipRow = null; if (tipForm != null) tipForm.Hide(); };
            FormClosing += delegate { SaveTags(); if (tipForm != null) tipForm.Close(); };
            Resize += delegate { RecalcHeight(); Invalidate(); };
        }
        int FooterH { get { return (int)(18 * S); } }
        int VisibleRows()
        {
            int avail = Height - HeaderH - FooterH;
            return Math.Max(1, avail / Math.Max(1, RowH));
        }
        int XpRowH { get { return (int)(18 * S); } }
        int TmrRowH { get { return (int)(17 * S); } }
        void RecalcHeight()
        {
            int maxH = Screen.FromControl(this).WorkingArea.Height * 8 / 10;
            int desired = tab == "xp"
                ? HeaderH + Math.Max(1, xpLines.Count) * XpRowH + FooterH
                : HeaderH + Math.Max(1, lastRows.Count) * RowH + FooterH;
            desired += timers.Count * TmrRowH;
            Height = Math.Min(desired, maxH);
            int maxScroll = Math.Max(0, lastRows.Count - VisibleRows());
            if (scroll > maxScroll) scroll = maxScroll;
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (tab == "xp") return;
            int maxScroll = Math.Max(0, lastRows.Count - VisibleRows());
            scroll = Math.Max(0, Math.Min(maxScroll, scroll - e.Delta / 120));
            Invalidate();
        }

        // borderless edge-resize (left/right edges)
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84) // WM_NCHITTEST
            {
                base.WndProc(ref m);
                Point p = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, (m.LParam.ToInt32() >> 16) & 0xFFFF));
                if (p.X >= Width - 6) { m.Result = (IntPtr)11; return; } // HTRIGHT
                if (p.X <= 6) { m.Result = (IntPtr)10; return; }          // HTLEFT
                return;
            }
            base.WndProc(ref m);
        }
        protected override void OnResizeEnd(EventArgs e) { base.OnResizeEnd(e); SaveTags(); }

        Rectangle CloseRect { get { return new Rectangle(Width - (int)(22 * S), (int)(3 * S), (int)(18 * S), (int)(20 * S)); } }
        Rectangle PauseRect { get { return new Rectangle(Width - (int)(42 * S), (int)(3 * S), (int)(18 * S), (int)(20 * S)); } }
        Rectangle ResetRect { get { return new Rectangle(Width - (int)(62 * S), (int)(3 * S), (int)(18 * S), (int)(20 * S)); } }
        Rectangle HelpRect { get { return new Rectangle(Width - (int)(82 * S), (int)(3 * S), (int)(18 * S), (int)(20 * S)); } }
        static string[] TABKEYS = { "dmg", "heal", "taken", "casts", "xp" };
        static string[] TABLABELS = { "DMG", "HEAL", "TAKE", "CAST", "XP" };
        Rectangle[] tabRects = new Rectangle[5];
        Rectangle viewRect;

        void OnDown(object s, MouseEventArgs e)
        {
            tipRow = null; if (tipForm != null) tipForm.Hide();
            if (e.Button == MouseButtons.Left)
            {
                if (PauseRect.Contains(e.Location)) { paused = !paused; Invalidate(); return; }
                if (CloseRect.Contains(e.Location)) { try { Close(); } catch { } Application.Exit(); return; }
                if (HelpRect.Contains(e.Location)) { ShowHelp(); return; }
                if (ResetRect.Contains(e.Location)) { resetAt = DateTimeOffset.Now.ToUnixTimeMilliseconds(); events.Clear(); fights.Clear(); casts.Clear(); xpEvents.Clear(); Aggregate(); RecalcHeight(); Invalidate(); return; }
                for (int i = 0; i < 5; i++)
                    if (tabRects[i].Contains(e.Location)) { tab = TABKEYS[i]; Aggregate(); RecalcHeight(); Invalidate(); return; }
                if (viewRect.Contains(e.Location)) { overallView = !overallView; Aggregate(); RecalcHeight(); Invalidate(); return; }
                dragging = true; dragOff = e.Location;
            }
            else if (e.Button == MouseButtons.Right) ShowMenu(e.Location);
        }
        void OnMove(object s, MouseEventArgs e)
        {
            if (dragging) { Location = new Point(Location.X + e.X - dragOff.X, Location.Y + e.Y - dragOff.Y); return; }
            string r = RowAt(e.Location);
            if (r != tipRow)
            {
                tipRow = r;
                if (r == null) { if (tipForm != null) tipForm.Hide(); return; }
                Row row = lastRows.FirstOrDefault(x => x.Name == r);
                if (row == null) return;
                if (tipForm == null) tipForm = new TipForm();
                tipForm.SetLines(BuildTipLines(row));
            }
            if (tipRow != null && tipForm != null)
            {
                Point sp = PointToScreen(new Point(e.X + 16, e.Y + 20));
                Rectangle wa = Screen.FromControl(this).WorkingArea;
                if (sp.X + tipForm.Width > wa.Right) sp.X = wa.Right - tipForm.Width;
                if (sp.Y + tipForm.Height > wa.Bottom) sp.Y = Math.Max(wa.Top, sp.Y - tipForm.Height - 44);
                tipForm.Location = sp;
                if (!tipForm.Visible) tipForm.Show();
            }
        }

        void StartTimer(string name, long ms)
        {
            Tmr tm = timers.FirstOrDefault(x => x.Name == name);
            if (tm == null) { tm = new Tmr { Name = name }; timers.Add(tm); }
            tm.End = DateTimeOffset.Now.ToUnixTimeMilliseconds() + ms;
            tm.Total = ms; tm.Ready = false;
            SaveTags(); RecalcHeight(); Invalidate();
        }
        static long ParseDur(string s)
        {
            try
            {
                s = s.Trim().ToLowerInvariant();
                if (s.Contains(":"))
                {
                    long sec = 0;
                    foreach (string p in s.Split(':')) sec = sec * 60 + long.Parse(p.Trim());
                    return sec * 1000;
                }
                MatchCollection mm = Regex.Matches(s, "(\\d+)\\s*([hms])");
                if (mm.Count > 0)
                {
                    long sec = 0;
                    foreach (Match m in mm)
                    {
                        long v = long.Parse(m.Groups[1].Value);
                        sec += m.Groups[2].Value == "h" ? v * 3600 : m.Groups[2].Value == "m" ? v * 60 : v;
                    }
                    return sec * 1000;
                }
                double mins;
                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out mins)) return (long)(mins * 60000);
            }
            catch { }
            return 0;
        }
        void PromptTimer()
        {
            Form d = new Form();
            d.Text = "Add timer"; d.FormBorderStyle = FormBorderStyle.FixedDialog; d.StartPosition = FormStartPosition.CenterParent;
            d.Width = 340; d.Height = 210; d.MaximizeBox = false; d.MinimizeBox = false; d.TopMost = true;
            Label l1 = new Label(); l1.Text = "Name (use the EXACT nano name for auto-restart):"; l1.Left = 12; l1.Top = 10; l1.Width = 300;
            TextBox tbN = new TextBox(); tbN.Left = 12; tbN.Top = 30; tbN.Width = 300;
            Label l2 = new Label(); l2.Text = "Duration  (45s, 90m, 6h, 6h30m, 1:30:00):"; l2.Left = 12; l2.Top = 58; l2.Width = 300;
            TextBox tbD = new TextBox(); tbD.Left = 12; tbD.Top = 78; tbD.Width = 300;
            CheckBox cb = new CheckBox(); cb.Text = "Restart automatically every time this nano lands"; cb.Left = 12; cb.Top = 106; cb.Width = 310;
            Button ok = new Button(); ok.Text = "Start"; ok.Left = 236; ok.Top = 136; ok.DialogResult = DialogResult.OK;
            d.Controls.Add(l1); d.Controls.Add(tbN); d.Controls.Add(l2); d.Controls.Add(tbD); d.Controls.Add(cb); d.Controls.Add(ok);
            d.AcceptButton = ok;
            if (d.ShowDialog(this) == DialogResult.OK)
            {
                string nm = tbN.Text.Trim(); long ms = ParseDur(tbD.Text);
                if (nm.Length == 0 || ms <= 0)
                { MessageBox.Show(this, "Need a name and a duration like 45s, 90m, 6h or 1:30:00.", "PRK Damage Meter"); return; }
                if (cb.Checked) nanoTimers[nm] = ms;
                StartTimer(nm, ms);
            }
        }

        string Pct(long part, long whole) { return "  (" + (100.0 * part / Math.Max(1, whole)).ToString("0") + "%)"; }
        List<string[]> BuildTipLines(Row row)
        {
            List<string[]> L = new List<string[]>();
            if (tab == "casts")
            {
                L.Add(new string[] { row.Name, "", "h" });
                L.Add(new string[] { "casts", row.Total.ToString(), "" });
                L.Add(new string[] { "landed", row.Hits.ToString(), "" });
                L.Add(new string[] { "resisted", row.Crits.ToString(), "" });
                if (row.Glances > 0) L.Add(new string[] { "interrupted", row.Glances.ToString(), "" });
                return L;
            }
            double durMin = Math.Max(1.0 / 60.0, lastDurMs / 60000.0);
            double durSec = Math.Max(1.0, lastDurMs / 1000.0);
            double share = lastGrand > 0 ? 100.0 * row.Total / lastGrand : 0;
            string prof; tags.TryGetValue(row.Name, out prof);
            L.Add(new string[] { row.Name + (prof != null ? "   (" + prof + ")" : ""), share.ToString("0.0") + "% of shown", "h" });
            L.Add(new string[] { "total", FmtN(row.Total) + "   " + FmtRate(row.Total / durSec), "" });
            L.Add(new string[] { "hits", row.Hits + "   (" + (row.Hits / durMin).ToString("0.0") + "/min)", "" });
            if (tab != "heal")
                L.Add(new string[] { "misses", row.Misses + "   (hit chance " + (100.0 * row.Hits / Math.Max(1, row.Hits + row.Misses)).ToString("0.0") + "%)", "" });
            L.Add(new string[] { "avg hit", FmtN(row.Hits > 0 ? row.Total / (double)row.Hits : 0), "" });
            L.Add(new string[] { "max hit", FmtN(row.Max), "" });
            L.Add(new string[] { "crits", row.Crits + "  (" + (100.0 * row.Crits / Math.Max(1, row.Hits)).ToString("0.0") + "%)", "" });
            L.Add(new string[] { "glances", row.Glances + "  (" + (100.0 * row.Glances / Math.Max(1, row.Hits)).ToString("0.0") + "%)", "" });
            if (tab != "heal" && (row.Weapon > 0 || row.Nano > 0 || row.Shield > 0))
            {
                L.Add(new string[] { "DAMAGE BY SOURCE", "", "s" });
                long specSum = 0;
                if (row.Specials != null) foreach (long[] v in row.Specials.Values) specSum += v[1];
                long regular = Math.Max(0, row.Weapon - specSum);
                if (regular > 0) L.Add(new string[] { "regular attacks", FmtN(regular) + Pct(regular, row.Total), "" });
                if (row.Specials != null)
                    foreach (KeyValuePair<string, long[]> kv in row.Specials.OrderByDescending(k => k.Value[1]))
                        L.Add(new string[] { kv.Key + (WEAPONSPECIALS.Contains(kv.Key) || kv.Key == "perk damage" ? "" : "  (perk)"),
                            kv.Value[0] + "x   " + FmtN(kv.Value[1]) + Pct(kv.Value[1], row.Total), "" });
                if (row.Nano > 0) L.Add(new string[] { "nano", FmtN(row.Nano) + Pct(row.Nano, row.Total), "" });
                if (row.Shield > 0) L.Add(new string[] { "damage shields", FmtN(row.Shield) + Pct(row.Shield, row.Total), "" });
                if (row.Types != null && row.Types.Count > 0)
                {
                    L.Add(new string[] { "DAMAGE TYPES", "", "s" });
                    foreach (KeyValuePair<string, long> kv in row.Types.OrderByDescending(k => k.Value))
                        L.Add(new string[] { kv.Key, FmtN(kv.Value) + Pct(kv.Value, row.Total), "" });
                }
            }
            if (row.Name == "Unknown")
            {
                L.Add(new string[] { "WHAT IS 'UNKNOWN'?", "", "s" });
                L.Add(new string[] { "Log lines that name nobody: actions by", "", "" });
                L.Add(new string[] { "characters OUTSIDE your team and some", "", "" });
                L.Add(new string[] { "heal-over-time ticks. Right-click the row", "", "" });
                L.Add(new string[] { "to hide it, or mark it as a pet of a player.", "", "" });
            }
            return L;
        }

        void ShowHelp()
        {
            Form d = new Form();
            d.FormBorderStyle = FormBorderStyle.None;
            d.TopMost = true; d.StartPosition = FormStartPosition.CenterScreen;
            d.Width = 560; d.Height = 660;
            d.BackColor = ColorTranslator.FromHtml("#0a1519");
            d.Paint += delegate(object ps, PaintEventArgs pa)
            { using (Pen p = new Pen(ColorTranslator.FromHtml("#2a4a56"))) pa.Graphics.DrawRectangle(p, 0, 0, d.Width - 1, d.Height - 1); };
            Panel head = new Panel(); head.Dock = DockStyle.Top; head.Height = 36;
            head.BackColor = ColorTranslator.FromHtml("#0d2027");
            Label ttl = new Label(); ttl.Text = "PRK  DAMAGE METER  -  HELP"; ttl.AutoSize = true;
            ttl.ForeColor = ColorTranslator.FromHtml("#5fd7e2"); ttl.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            ttl.Location = new Point(12, 8); ttl.BackColor = Color.Transparent;
            Label xx = new Label(); xx.Text = "X"; xx.AutoSize = true;
            xx.ForeColor = ColorTranslator.FromHtml("#7e9094"); xx.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            xx.Location = new Point(d.Width - 32, 8); xx.Cursor = Cursors.Hand; xx.BackColor = Color.Transparent;
            xx.Click += delegate { d.Close(); };
            head.Controls.Add(ttl); head.Controls.Add(xx);
            Point hOff = Point.Empty; bool hDrag = false;
            head.MouseDown += delegate(object hs, MouseEventArgs he) { hDrag = true; hOff = he.Location; };
            head.MouseMove += delegate(object hs, MouseEventArgs he) { if (hDrag) d.Location = new Point(d.Location.X + he.X - hOff.X, d.Location.Y + he.Y - hOff.Y); };
            head.MouseUp += delegate { hDrag = false; };
            Panel wrap = new Panel(); wrap.Dock = DockStyle.Fill; wrap.Padding = new Padding(16, 12, 10, 12);
            wrap.BackColor = ColorTranslator.FromHtml("#0a1519");
            TextBox tb = new TextBox();
            tb.Multiline = true; tb.ReadOnly = true; tb.ScrollBars = ScrollBars.Vertical; tb.Dock = DockStyle.Fill;
            tb.BackColor = ColorTranslator.FromHtml("#0a1519"); tb.ForeColor = ColorTranslator.FromHtml("#dee8ea");
            tb.BorderStyle = BorderStyle.None; tb.Font = new Font("Segoe UI", 9.5f);
            tb.Text =
"THE WINDOW\r\n" +
"  DMG   damage done      HEAL  healing done\r\n" +
"  TAKE  damage taken     CAST  your nano casts\r\n" +
"  XP    xp per hour session tracker\r\n" +
"  fight / all  - bottom-left toggle: last fight vs everything\r\n" +
"  ? help   R reset   || pause   X quit\r\n" +
"  Drag anywhere to move. Drag left/right edge to resize.\r\n" +
"  Hover a bar for full details: hits/min, avg + max hit, crit and\r\n" +
"  glance %, weapon/nano/shield split, damage types (melee, cold,\r\n" +
"  poison...) and specials (Burst, Fling Shot...).\r\n" +
"  Green dot (bottom-left) = watching your log live.\r\n\r\n" +
"TIMERS (trimmers, perks, buffs, anything)\r\n" +
"  Countdown bars appear above the footer on every tab and flash\r\n" +
"  READY (green) when done. They start automatically from:\r\n" +
"  - 'Cannot use the [skill] on this target for another X seconds'\r\n" +
"  - 'Unable to perform action, X skill/perk is locked, able in ...'\r\n" +
"  Click the locked hotbar button once to (re)start those.\r\n" +
"  Right-click > Timers > Add timer... for manual ones - and if the\r\n" +
"  name matches a nano EXACTLY (e.g. Shadowweb Spinner MK V) you can\r\n" +
"  make it restart automatically every time that nano lands. Great\r\n" +
"  for 6h buffs. Timers survive restarting the meter.\r\n\r\n" +
"MISSES\r\n" +
"  Missed and parried attacks are counted: hover a bar for miss\r\n" +
"  count + hit chance, and /prkdmg includes misses per player.\r\n\r\n" +
"THE 'UNKNOWN' ROW\r\n" +
"  Some log lines name nobody: actions by characters OUTSIDE your\r\n" +
"  team (an unteamed follower, a passer-by) and some heal-over-time\r\n" +
"  ticks. The game just doesn't say who, so they all pool into one\r\n" +
"  'Unknown' row. Right-click it to hide it permanently, or use\r\n" +
"  'Mark as pet of' to credit a specific player (e.g. your box doc).\r\n\r\n" +
"XP TAB\r\n" +
"  Tracks xp, Shadowknowledge and Alien XP from your first xp tick\r\n" +
"  after the meter starts. Shows totals, per-hour rates, a rolling\r\n" +
"  5-minute pace (your current speed vs session average), kill\r\n" +
"  counts, avg + best tick, deaths and net xp. SK / AXP sections\r\n" +
"  appear once you earn some. R resets the session (and all data).\r\n" +
"  Right-click > 'Copy summary' copies the whole XP report as text.\r\n\r\n" +
"FIGHTS\r\n" +
"  A fight ends after 6 seconds without combat, but stays on screen\r\n" +
"  until the NEXT fight starts (or you hit R). 'all' keeps everything\r\n" +
"  since the last reset - use it for boss fights and full sessions.\r\n\r\n" +
"RIGHT-CLICK MENU\r\n" +
"  Click a bar first to: set a profession (class color), mark it as\r\n" +
"  someone's pet (damage rolls into the owner), or hide it (mob/NPC).\r\n" +
"  Also: set your character name, opacity, choose log, reset, exit.\r\n\r\n" +
"CHAT COMMANDS (share your numbers in game)\r\n" +
"  /prkdmg   - post damage rankings (follows your fight/all toggle)\r\n" +
"  /prkheal  - post healing rankings (follows the toggle too)\r\n" +
"  /prkcast  - post your nano cast counts (landed / resisted)\r\n" +
"  Tip: make a hotbar macro once:   /macro dmg /prkdmg\r\n\r\n" +
"AUTOMATIC STUFF\r\n" +
"  - First run creates the logged 'Damage' chat window for all your\r\n" +
"    characters (re-run from right-click menu for new alts).\r\n" +
"  - Professions auto-detect from PROFESSION-LOCKED nanos (2,900+\r\n" +
"    nano database): your casts tag you; teammates' buffs landing in\r\n" +
"    your NCU tag them. Generic buffs (Composites etc.) carry no\r\n" +
"    profession info, so those never trigger a tag.\r\n" +
"  - Pets named like \"Dylan's robot\" auto-credit their owner\r\n" +
"    (when the owner is a known player). Others: mark manually.\r\n" +
"  - CAST tab counts interrupted/countered/fumbled casts too.\r\n" +
"  - Mob-like names (with spaces) are auto-hidden from rankings\r\n" +
"    (toggle in right-click menu). Player names never contain spaces.\r\n" +
"  - All tags and settings are remembered between sessions.\r\n\r\n" +
"A PRK community tool by Everkill  -  .everkill on Discord";
            wrap.Controls.Add(tb);
            d.Controls.Add(wrap); d.Controls.Add(head);
            d.Show();
            tb.SelectionLength = 0; tb.SelectionStart = 0;
        }

        string RowAt(Point p)
        {
            if (tab == "xp") return null;
            int idx = scroll + (p.Y - HeaderH) / RowH;
            if (p.Y >= HeaderH && idx >= scroll && idx < lastRows.Count) return lastRows[idx].Name;
            return null;
        }

        void PromptMyName()
        {
            Form d = new Form();
            d.Text = "Character name"; d.FormBorderStyle = FormBorderStyle.FixedDialog; d.StartPosition = FormStartPosition.CenterParent;
            d.Width = 280; d.Height = 120; d.MaximizeBox = false; d.MinimizeBox = false; d.TopMost = true;
            TextBox tb = new TextBox(); tb.Left = 12; tb.Top = 12; tb.Width = 240; tb.Text = myName == "You" ? "" : myName;
            Button ok = new Button(); ok.Text = "OK"; ok.Left = 176; ok.Top = 44; ok.DialogResult = DialogResult.OK;
            d.Controls.Add(tb); d.Controls.Add(ok); d.AcceptButton = ok;
            if (d.ShowDialog(this) == DialogResult.OK && tb.Text.Trim().Length > 0)
            {
                string old = myName; myName = tb.Text.Trim();
                foreach (Ev ev in events) { if (ev.Src == old) ev.Src = myName; if (ev.Dst == old) ev.Dst = myName; }
                if (tags.ContainsKey(old)) { tags[myName] = tags[old]; tags.Remove(old); }
                SaveTags(); Aggregate(); Invalidate();
            }
        }

        string BuildReport()
        {
            if (tab == "xp")
            {
                List<string> xs = new List<string>();
                string sect = "";
                foreach (string[] ln in xpLines)
                {
                    if (ln[1] == "") sect = ln[0] + " ";
                    else if (ln[1] != " ") xs.Add(sect + ln[0] + ": " + ln[1]);
                }
                return "PRK XP session — " + string.Join(", ", xs.ToArray());
            }
            StringBuilder sb = new StringBuilder();
            string what = tab == "dmg" ? "Damage" : tab == "heal" ? "Healing" : "Damage taken";
            sb.Append(what + " (" + (overallView ? "overall" : "last fight") + ", " + (lastDurMs / 1000) + "s): ");
            List<string> parts = new List<string>();
            foreach (Row r in lastRows.Take(6))
                parts.Add(r.Name + " " + FmtN(r.Total) + " (" + FmtN(r.Total / (lastDurMs / 1000.0)) + "/s)");
            sb.Append(string.Join(", ", parts.ToArray()));
            return sb.ToString();
        }

        void ShowMenu(Point at)
        {
            string row = RowAt(at);
            ContextMenuStrip m = new ContextMenuStrip();
            if (row != null)
            {
                ToolStripMenuItem prof = new ToolStripMenuItem("Profession: " + row);
                foreach (string p in PROF_NAMES)
                {
                    string pp = p;
                    ToolStripMenuItem it = new ToolStripMenuItem(p);
                    it.Checked = tags.ContainsKey(row) && tags[row] == p;
                    it.Click += delegate { tags[row] = pp; SaveTags(); Aggregate(); Invalidate(); };
                    prof.DropDownItems.Add(it);
                }
                m.Items.Add(prof);
                ToolStripMenuItem pet = new ToolStripMenuItem("Mark as pet of");
                List<string> names = events.Where(e2 => e2.Kind == "dmg").Select(e2 => OwnerOf(e2.Src)).Distinct()
                    .Where(n => n != row).OrderBy(n => n).ToList();
                ToolStripMenuItem none = new ToolStripMenuItem("(not a pet)");
                none.Click += delegate { petOwner.Remove(row); SaveTags(); Aggregate(); Invalidate(); };
                pet.DropDownItems.Add(none);
                foreach (string n in names)
                {
                    string nn = n;
                    ToolStripMenuItem it = new ToolStripMenuItem(n);
                    it.Checked = petOwner.ContainsKey(row) && petOwner[row] == n;
                    it.Click += delegate { petOwner[row] = nn; SaveTags(); Aggregate(); RecalcHeight(); Invalidate(); };
                    pet.DropDownItems.Add(it);
                }
                m.Items.Add(pet);
                ToolStripMenuItem hide = new ToolStripMenuItem("Hide " + row + " (NPC/mob)");
                hide.Click += delegate { hidden.Add(row); SaveTags(); Aggregate(); RecalcHeight(); Invalidate(); };
                m.Items.Add(hide);
                m.Items.Add(new ToolStripSeparator());
            }
            ToolStripMenuItem tDmg = new ToolStripMenuItem("Damage done"); tDmg.Checked = tab == "dmg";
            tDmg.Click += delegate { tab = "dmg"; Aggregate(); RecalcHeight(); Invalidate(); };
            ToolStripMenuItem tHeal = new ToolStripMenuItem("Healing done"); tHeal.Checked = tab == "heal";
            tHeal.Click += delegate { tab = "heal"; Aggregate(); RecalcHeight(); Invalidate(); };
            ToolStripMenuItem tTaken = new ToolStripMenuItem("Damage taken"); tTaken.Checked = tab == "taken";
            tTaken.Click += delegate { tab = "taken"; Aggregate(); RecalcHeight(); Invalidate(); };
            ToolStripMenuItem tCasts = new ToolStripMenuItem("My nano casts"); tCasts.Checked = tab == "casts";
            tCasts.Click += delegate { tab = "casts"; Aggregate(); RecalcHeight(); Invalidate(); };
            ToolStripMenuItem tXp = new ToolStripMenuItem("XP / hour"); tXp.Checked = tab == "xp";
            tXp.Click += delegate { tab = "xp"; Aggregate(); RecalcHeight(); Invalidate(); };
            m.Items.Add(tDmg); m.Items.Add(tHeal); m.Items.Add(tTaken); m.Items.Add(tCasts); m.Items.Add(tXp);
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem vCur = new ToolStripMenuItem("Current fight"); vCur.Checked = !overallView;
            vCur.Click += delegate { overallView = false; Aggregate(); RecalcHeight(); Invalidate(); };
            ToolStripMenuItem vAll = new ToolStripMenuItem("Overall"); vAll.Checked = overallView;
            vAll.Click += delegate { overallView = true; Aggregate(); RecalcHeight(); Invalidate(); };
            m.Items.Add(vCur); m.Items.Add(vAll);
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem myn = new ToolStripMenuItem("Set my character name...  (" + myName + ")");
            myn.Click += delegate { PromptMyName(); };
            m.Items.Add(myn);
            ToolStripMenuItem opac = new ToolStripMenuItem("Opacity");
            foreach (int o in new int[] { 80, 90, 95, 100 })
            {
                int oo = o;
                ToolStripMenuItem it = new ToolStripMenuItem(o + "%");
                it.Checked = Math.Abs(Opacity * 100 - o) < 3;
                it.Click += delegate { Opacity = oo / 100.0; };
                opac.DropDownItems.Add(it);
            }
            m.Items.Add(opac);
            ToolStripMenuItem cpy = new ToolStripMenuItem("Copy summary to clipboard");
            cpy.Click += delegate { try { Clipboard.SetText(BuildReport()); } catch { } };
            m.Items.Add(cpy);
            ToolStripMenuItem dpmT = new ToolStripMenuItem("Show damage per minute (DPM)");
            dpmT.Checked = showDpm;
            dpmT.Click += delegate { showDpm = !showDpm; SaveTags(); Aggregate(); Invalidate(); };
            m.Items.Add(dpmT);
            ToolStripMenuItem amob = new ToolStripMenuItem("Auto-hide mobs (multi-word names)");
            amob.Checked = autoHideMobs;
            amob.Click += delegate { autoHideMobs = !autoHideMobs; SaveTags(); Aggregate(); RecalcHeight(); Invalidate(); };
            m.Items.Add(amob);
            ToolStripMenuItem unhide = new ToolStripMenuItem("Unhide all");
            unhide.Click += delegate { hidden.Clear(); SaveTags(); Aggregate(); RecalcHeight(); Invalidate(); };
            m.Items.Add(unhide);
            ToolStripMenuItem tmrs = new ToolStripMenuItem("Timers");
            ToolStripMenuItem addT = new ToolStripMenuItem("Add timer...  (manual or auto-on-nano)");
            addT.Click += delegate { PromptTimer(); };
            tmrs.DropDownItems.Add(addT);
            if (timers.Count > 0 || nanoTimers.Count > 0) tmrs.DropDownItems.Add(new ToolStripSeparator());
            long nowMs2 = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            foreach (Tmr tm in timers.ToList())
            {
                Tmr tt = tm;
                ToolStripMenuItem it = new ToolStripMenuItem("Cancel: " + tm.Name + "  (" + FmtDur(Math.Max(0, tm.End - nowMs2)) + " left)");
                it.Click += delegate { timers.Remove(tt); SaveTags(); RecalcHeight(); Invalidate(); };
                tmrs.DropDownItems.Add(it);
            }
            foreach (KeyValuePair<string, long> kv in nanoTimers.ToList())
            {
                string nn2 = kv.Key;
                ToolStripMenuItem it = new ToolStripMenuItem("Remove auto-timer: " + kv.Key + "  (" + FmtDur(kv.Value) + ")");
                it.Click += delegate { nanoTimers.Remove(nn2); SaveTags(); };
                tmrs.DropDownItems.Add(it);
            }
            m.Items.Add(tmrs);
            ToolStripMenuItem setup = new ToolStripMenuItem("Set up in-game chat window...");
            setup.Click += delegate { SetupGameWindows(false); };
            m.Items.Add(setup);
            ToolStripMenuItem pick = new ToolStripMenuItem("Choose log file...");
            pick.Click += delegate
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "AO chat log|*.txt";
                if (d.ShowDialog() == DialogResult.OK) { logPath = d.FileName; lastPos = 0; carry = ""; events.Clear(); fights.Clear(); xpEvents.Clear(); Aggregate(); Invalidate(); }
            };
            m.Items.Add(pick);
            ToolStripMenuItem rescan = new ToolStripMenuItem("Auto-detect log");
            rescan.Click += delegate { logPath = FindLog(); lastPos = 0; carry = ""; events.Clear(); fights.Clear(); xpEvents.Clear(); Aggregate(); Invalidate(); };
            m.Items.Add(rescan);
            ToolStripMenuItem loginfo = new ToolStripMenuItem("Log file info...");
            loginfo.Click += delegate
            {
                MessageBox.Show(this,
                    "Watching:\n" + (logPath ?? "(no log found)") +
                    "\n\nEvents parsed: " + events.Count + "\nFights: " + fights.Count + "\nNano casts: " + casts.Count +
                    "\n\nIf events stay at 0 while you fight, the meter is watching the wrong file - " +
                    "use 'Choose log file...' and pick the Log.txt inside your Damage window's folder " +
                    "(Chat/Windows/WindowN under your character's prefs).",
                    "PRK Damage Meter - log info");
            };
            m.Items.Add(loginfo);
            ToolStripMenuItem reset = new ToolStripMenuItem("Reset data");
            reset.Click += delegate { resetAt = DateTimeOffset.Now.ToUnixTimeMilliseconds(); events.Clear(); fights.Clear(); casts.Clear(); xpEvents.Clear(); Aggregate(); RecalcHeight(); Invalidate(); };
            m.Items.Add(reset);
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
            exit.Click += delegate { Close(); };
            m.Items.Add(exit);
            m.Show(this, at);
        }

        string FmtRate(double perSec) { return showDpm ? FmtN(perSec * 60) + "/m" : FmtN(perSec) + "/s"; }
        static string FmtN(double n)
        {
            if (n >= 1000000) return (n / 1000000.0).ToString("0.00", CultureInfo.InvariantCulture) + "M";
            if (n >= 10000) return (n / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "K";
            return ((long)Math.Round(n)).ToString("N0", CultureInfo.InvariantCulture);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = S;
            Font fName = new Font("Segoe UI", 9.5f * s, FontStyle.Bold);
            Font fSmall = new Font("Segoe UI", 8.2f * s);
            Font fSmallB = new Font("Segoe UI", 8.2f * s, FontStyle.Bold);
            Font fChip = new Font("Segoe UI", 7.0f * s, FontStyle.Bold);
            Brush dim = new SolidBrush(ColorTranslator.FromHtml("#7e9094"));
            Brush txt = new SolidBrush(ColorTranslator.FromHtml("#dee8ea"));
            Brush cyan = new SolidBrush(ColorTranslator.FromHtml("#5fd7e2"));
            Brush gold = new SolidBrush(ColorTranslator.FromHtml("#f2cc79"));
            // header
            using (Brush hb = new SolidBrush(ColorTranslator.FromHtml("#0d2027"))) g.FillRectangle(hb, 0, 0, Width, HeaderH);
            using (Pen pen = new Pen(ColorTranslator.FromHtml("#1d3540"))) g.DrawLine(pen, 0, HeaderH - 1, Width, HeaderH - 1);
            g.DrawString("PRK", fName, cyan, 5 * s, 4 * s);
            float tx = 36 * s;
            for (int i = 0; i < 5; i++)
            {
                bool on = tab == TABKEYS[i];
                SizeF tsz = g.MeasureString(TABLABELS[i], fSmall);
                tabRects[i] = new Rectangle((int)tx, (int)(3 * s), (int)(tsz.Width + 6 * s), (int)(20 * s));
                g.DrawString(TABLABELS[i], fSmall, on ? cyan : dim, tx + 3 * s, 5 * s);
                if (on) using (Pen up = new Pen(ColorTranslator.FromHtml("#5fd7e2"), Math.Max(1f, 1.5f * s)))
                    g.DrawLine(up, tx + 2 * s, HeaderH - 4 * s, tx + tsz.Width + 4 * s, HeaderH - 4 * s);
                tx += tsz.Width + 9 * s;
            }
            // ? / reset / pause / close buttons
            g.DrawString("?", fName, dim, HelpRect.X + 4 * s, HelpRect.Y + 2 * s);
            g.DrawString("R", fName, dim, ResetRect.X + 4 * s, ResetRect.Y + 2 * s);
            Brush pb = paused ? gold : dim;
            if (paused) g.DrawString(">", fName, pb, PauseRect.X + 4 * s, PauseRect.Y + 2 * s);
            else { g.FillRectangle(pb, PauseRect.X + 4 * s, PauseRect.Y + 5 * s, 4 * s, 11 * s); g.FillRectangle(pb, PauseRect.X + 11 * s, PauseRect.Y + 5 * s, 4 * s, 11 * s); }
            g.DrawString("X", fName, dim, CloseRect.X + 4 * s, CloseRect.Y + 2 * s);
            bool live = logPath != null && File.Exists(logPath) && !paused;
            int y = HeaderH;
            if (tab == "xp")
            {
                Font fSect = new Font("Segoe UI", 8.2f * s, FontStyle.Bold);
                foreach (string[] ln in xpLines)
                {
                    if (ln[1] == "")
                    {
                        g.DrawString(ln[0], fSect, gold, 8 * s, y + 4 * s);
                        using (Pen pen = new Pen(ColorTranslator.FromHtml("#1d3540")))
                            g.DrawLine(pen, 8 * s, y + XpRowH - 2 * s, Width - 8 * s, y + XpRowH - 2 * s);
                    }
                    else
                    {
                        g.DrawString(ln[0], fSmall, dim, 14 * s, y + 3 * s);
                        bool rate = ln[1].EndsWith("/h");
                        SizeF vz = g.MeasureString(ln[1], fSmall);
                        g.DrawString(ln[1], fSmall, rate ? cyan : txt, Width - 8 * s - vz.Width, y + 3 * s);
                    }
                    y += XpRowH;
                }
                fSect.Dispose();
                y = DrawTimers(g, s, y, fSmall);
                DrawFooter(g, s, y, live, fSmall, gold, "xp session  •  R resets");
                fName.Dispose(); fSmall.Dispose(); fSmallB.Dispose(); fChip.Dispose(); dim.Dispose(); txt.Dispose(); cyan.Dispose(); gold.Dispose();
                return;
            }
            if (lastRows.Count == 0)
            {
                g.DrawString(live ? "waiting for combat..." : (paused ? "paused" : "no log found — right-click"), fSmall, dim, 8 * s, y + 5 * s);
                y += RowH; // keep the footer on its own line below the message
            }
            long top = lastRows.Count > 0 ? lastRows[0].Total : 1;
            int vis = VisibleRows();
            int last = Math.Min(lastRows.Count, scroll + vis);
            for (int i = scroll; i < last; i++)
            {
                Row r = lastRows[i];
                Rectangle rowRect = new Rectangle((int)(4 * s), y + (int)(1 * s), Width - (int)(8 * s), RowH - (int)(3 * s));
                using (Brush bg = new SolidBrush(ColorTranslator.FromHtml("#0c181d"))) g.FillRectangle(bg, rowRect);
                string prof; if (!tags.TryGetValue(r.Name, out prof)) prof = "Unknown";
                Color pc = tab == "casts" ? ColorTranslator.FromHtml("#2a5a66") : PROF_COLOR[prof];
                int fw = (int)((Width - 8 * s) * (double)r.Total / top);
                Rectangle fillRect = new Rectangle((int)(4 * s), y + (int)(1 * s), Math.Max((int)(2 * s), fw), RowH - (int)(3 * s));
                using (Brush fill = new LinearGradientBrush(rowRect, Color.FromArgb(205, pc), Color.FromArgb(105, pc), LinearGradientMode.Vertical))
                    g.FillRectangle(fill, fillRect);
                // rank
                g.DrawString((i + 1).ToString(), fSmall, dim, 7 * s, y + 5 * s);
                // prof icon (real in-game icons) or fallback chip (not on the casts tab)
                float nameX = 26 * s;
                if (tab != "casts")
                {
                    Image icn;
                    if (prof != "Unknown" && profIcons.TryGetValue(prof, out icn))
                    {
                        InterpolationMode om = g.InterpolationMode;
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(icn, 21 * s, y + 3.5f * s, 16 * s, 16 * s);
                        g.InterpolationMode = om;
                        nameX = 41 * s;
                    }
                    else
                    {
                        RectangleF chip = new RectangleF(20 * s, y + 4.5f * s, 24 * s, 14 * s);
                        using (Brush cb = new SolidBrush(pc)) g.FillRectangle(cb, chip.X, chip.Y, chip.Width, chip.Height);
                        g.DrawString(PROF_ABBR[prof], fChip, Brushes.Black, chip.X + 2 * s, chip.Y + 1.5f * s);
                        nameX = 48 * s;
                    }
                }
                // value first (so the name can be clipped against it) — rate drawn bold for visibility
                double dps = r.Total / (lastDurMs / 1000.0);
                double pct = lastGrand > 0 ? 100.0 * r.Total / lastGrand : 0;
                SizeF sz;
                if (tab == "casts")
                {
                    string val = r.Total + "x  " + r.Hits + " landed  " + r.Crits + " res" + (r.Glances > 0 ? "  " + r.Glances + " int" : "");
                    sz = g.MeasureString(val, fSmall);
                    g.DrawString(val, fSmall, txt, Width - 8 * s - sz.Width, y + 5 * s);
                }
                else
                {
                    string vTot = FmtN(r.Total) + " ";
                    string vRate = FmtRate(dps);
                    string vPct = " " + pct.ToString("0") + "%";
                    SizeF szT = g.MeasureString(vTot, fSmall);
                    SizeF szR = g.MeasureString(vRate, fSmallB);
                    SizeF szP = g.MeasureString(vPct, fSmall);
                    float xP = Width - 8 * s - szP.Width;
                    float xRt = xP - szR.Width;
                    float xT = xRt - szT.Width;
                    g.DrawString(vTot, fSmall, txt, xT, y + 5 * s);
                    g.DrawString(vRate, fSmallB, txt, xRt, y + 5 * s);
                    g.DrawString(vPct, fSmall, dim, xP, y + 5 * s);
                    sz = new SizeF(szT.Width + szR.Width + szP.Width, szT.Height);
                }
                // name, ellipsized to the space left of the value
                string nm = r.Name + (r.HasPets ? " +pet" : "");
                RectangleF nameRect = new RectangleF(nameX, y + 3 * s, Width - 8 * s - sz.Width - nameX - 4 * s, RowH);
                using (StringFormat sf = new StringFormat())
                {
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(nm, fName, txt, nameRect, sf);
                }
                y += RowH;
            }
            // total line
            int below = lastRows.Count - last;
            string tot = (below > 0 ? "v +" + below + " more (scroll)   " : (scroll > 0 ? "^ scroll up   " : "")) + "total " + FmtN(lastGrand) + "  •  " + (lastDurMs / 1000) + "s";
            y = DrawTimers(g, s, y, fSmall);
            DrawFooter(g, s, y, live, fSmall, gold, tot);
            fName.Dispose(); fSmall.Dispose(); fSmallB.Dispose(); fChip.Dispose(); dim.Dispose(); txt.Dispose(); cyan.Dispose(); gold.Dispose();
        }

        // skill-lock countdown bars (trimmers etc.) — shown on every tab above the footer
        int DrawTimers(Graphics g, float s, int y, Font fSmall)
        {
            if (timers.Count == 0) return y;
            long nowMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            foreach (Tmr t in timers.OrderBy(x => x.End).ToList())
            {
                double remMs = t.End - nowMs;
                bool ready = t.Ready || remMs <= 0;
                Rectangle rowRect = new Rectangle((int)(4 * s), y + (int)(1 * s), Width - (int)(8 * s), TmrRowH - (int)(2 * s));
                using (Brush bg = new SolidBrush(ColorTranslator.FromHtml("#0c181d"))) g.FillRectangle(bg, rowRect);
                Color c = ready ? Color.FromArgb(84, 224, 106) : Color.FromArgb(242, 204, 121);
                if (!ready && t.Total > 0)
                {
                    int fw = (int)(rowRect.Width * Math.Min(1.0, remMs / (double)t.Total));
                    using (Brush fill = new SolidBrush(Color.FromArgb(60, c)))
                        g.FillRectangle(fill, rowRect.X, rowRect.Y, Math.Max((int)(2 * s), fw), rowRect.Height);
                }
                using (Brush nb = new SolidBrush(c))
                {
                    g.DrawString(t.Name, fSmall, nb, 8 * s, y + 2 * s);
                    string rem = ready ? "READY" : FmtDur((long)Math.Max(0, remMs));
                    SizeF rs = g.MeasureString(rem, fSmall);
                    g.DrawString(rem, fSmall, nb, Width - 8 * s - rs.Width, y + 2 * s);
                }
                y += TmrRowH;
            }
            return y;
        }

        // footer: live dot + fight/all toggle on the left, totals on the right
        void DrawFooter(Graphics g, float s, int y, bool live, Font fSmall, Brush gold, string rightText)
        {
            using (Brush b = new SolidBrush(live ? Color.FromArgb(84, 224, 106) : (paused ? Color.FromArgb(242, 204, 121) : Color.Gray)))
                g.FillEllipse(b, 7 * s, y + 6 * s, 7 * s, 7 * s);
            string view = overallView ? "all" : "fight";
            SizeF vsz = g.MeasureString(view, fSmall);
            viewRect = new Rectangle((int)(17 * s), y, (int)(vsz.Width + 8 * s), FooterH);
            g.DrawString(view, fSmall, gold, 20 * s, y + 2 * s);
            SizeF ts = g.MeasureString(rightText, fSmall);
            g.DrawString(rightText, fSmall, gold, Width - 8 * s - ts.Width, y + 2 * s);
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new MeterForm());
        }
    }
}
