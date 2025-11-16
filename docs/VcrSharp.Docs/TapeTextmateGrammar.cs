namespace VcrSharp.Docs;

internal static class TapeTextmateGrammar
{
    public const string Grammar = """
                                  {
                                    "$schema": "https://raw.githubusercontent.com/martinring/tmlanguage/master/tmlanguage.json",
                                    "name": "VcrSharp Tape",
                                    "scopeName": "source.tape",
                                    "patterns": [
                                      {
                                        "include": "#comments"
                                      },
                                      {
                                        "include": "#commands"
                                      }
                                    ],
                                    "repository": {
                                      "comments": {
                                        "patterns": [
                                          {
                                            "name": "comment.line.number-sign.tape",
                                            "match": "#.*$"
                                          }
                                        ]
                                      },
                                      "commands": {
                                        "patterns": [
                                          {
                                            "include": "#set-command"
                                          },
                                          {
                                            "include": "#output-command"
                                          },
                                          {
                                            "include": "#require-command"
                                          },
                                          {
                                            "include": "#source-command"
                                          },
                                          {
                                            "include": "#type-command"
                                          },
                                          {
                                            "include": "#exec-command"
                                          },
                                          {
                                            "include": "#sleep-command"
                                          },
                                          {
                                            "include": "#wait-command"
                                          },
                                          {
                                            "include": "#hide-show-commands"
                                          },
                                          {
                                            "include": "#screenshot-command"
                                          },
                                          {
                                            "include": "#copy-paste-commands"
                                          },
                                          {
                                            "include": "#env-command"
                                          },
                                          {
                                            "include": "#modifier-command"
                                          },
                                          {
                                            "include": "#key-command"
                                          }
                                        ]
                                      },
                                      "set-command": {
                                        "name": "meta.command.set.tape",
                                        "match": "\\b(Set)\\s+(Width|Height|Cols|Rows|FontSize|FontFamily|LetterSpacing|LineHeight|Framerate|PlaybackSpeed|LoopOffset|MaxColors|Theme|Padding|Margin|MarginFill|WindowBarSize|BorderRadius|CursorBlink|TransparentBackground|Shell|WorkingDirectory|TypingSpeed|WaitTimeout|WaitPattern|InactivityTimeout|StartWaitTimeout|StartBuffer|EndBuffer)\\s+(.+)$",
                                        "captures": {
                                          "1": {
                                            "name": "keyword.control.set.tape"
                                          },
                                          "2": {
                                            "name": "variable.parameter.setting.tape"
                                          },
                                          "3": {
                                            "patterns": [
                                              {
                                                "include": "#string"
                                              },
                                              {
                                                "include": "#duration"
                                              },
                                              {
                                                "include": "#number"
                                              },
                                              {
                                                "include": "#boolean"
                                              }
                                            ]
                                          }
                                        }
                                      },
                                      "output-command": {
                                        "name": "meta.command.output.tape",
                                        "match": "\\b(Output)\\s+(.+)$",
                                        "captures": {
                                          "1": {
                                            "name": "keyword.control.output.tape"
                                          },
                                          "2": {
                                            "patterns": [
                                              {
                                                "include": "#string"
                                              },
                                              {
                                                "name": "string.unquoted.filepath.tape",
                                                "match": "[^\\s]+"
                                              }
                                            ]
                                          }
                                        }
                                      },
                                      "require-command": {
                                        "name": "meta.command.require.tape",
                                        "match": "\\b(Require)\\s+(\\w+)",
                                        "captures": {
                                          "1": {
                                            "name": "keyword.control.require.tape"
                                          },
                                          "2": {
                                            "name": "entity.name.program.tape"
                                          }
                                        }
                                      },
                                      "source-command": {
                                        "name": "meta.command.source.tape",
                                        "match": "\\b(Source)\\s+(.+)$",
                                        "captures": {
                                          "1": {
                                            "name": "keyword.control.source.tape"
                                          },
                                          "2": {
                                            "patterns": [
                                              {
                                                "include": "#string"
                                              },
                                              {
                                                "name": "string.unquoted.filepath.tape",
                                                "match": "[^\\s]+"
                                              }
                                            ]
                                          }
                                        }
                                      },
                                      "type-command": {
                                        "name": "meta.command.type.tape",
                                        "match": "\\b(Type)((?:@[0-9.]+(?:ms|s|m))?)\\s+",
                                        "captures": {
                                          "1": {
                                            "name": "keyword.control.type.tape"
                                          },
                                          "2": {
                                            "patterns": [
                                              {
                                                "include": "#speed-modifier"
                                              }
                                            ]
                                          }
                                        }
                                      },
                                      "exec-command": {
                                        "name": "meta.command.exec.tape",
                                        "match": "\\b(Exec)\\s+",
                                        "captures": {
                                          "1": {
                                            "name": "keyword.control.exec.tape"
                                          }
                                        }
                                      },
                                      "sleep-command": {
                                        "name": "meta.command.sleep.tape",
                                        "match": "\\b(Sleep)\\s+([0-9.]+(?:ms|s|m)?)",
                                        "captures": {
                                          "1": {
                                            "name": "keyword.control.sleep.tape"
                                          },
                                          "2": {
                                            "patterns": [
                                              {
                                                "include": "#duration"
                                              },
                                              {
                                                "include": "#number"
                                              }
                                            ]
                                          }
                                        }
                                      },
                                      "wait-command": {
                                        "name": "meta.command.wait.tape",
                                        "match": "\\b(Wait)((?:\\+(?:Screen|Buffer|Line))?)((?:@[0-9.]+(?:ms|s|m))?)?\\s*(.*)$",
                                        "captures": {
                                          "1": {
                                            "name": "keyword.control.wait.tape"
                                          },
                                          "2": {
                                            "name": "constant.language.scope.tape"
                                          },
                                          "3": {
                                            "patterns": [
                                              {
                                                "include": "#speed-modifier"
                                              }
                                            ]
                                          },
                                          "4": {
                                            "patterns": [
                                              {
                                                "include": "#regex"
                                              }
                                            ]
                                          }
                                        }
                                      },
                                      "hide-show-commands": {
                                        "name": "meta.command.hide-show.tape",
                                        "match": "\\b(Hide|Show)\\b",
                                        "captures": {
                                          "1": {
                                            "name": "keyword.control.visibility.tape"
                                          }
                                        }
                                      },
                                      "screenshot-command": {
                                        "name": "meta.command.screenshot.tape",
                                        "match": "\\b(Screenshot)\\s+(.+)$",
                                        "captures": {
                                          "1": {
                                            "name": "keyword.control.screenshot.tape"
                                          },
                                          "2": {
                                            "patterns": [
                                              {
                                                "include": "#string"
                                              },
                                              {
                                                "name": "string.unquoted.filepath.tape",
                                                "match": "[^\\s]+"
                                              }
                                            ]
                                          }
                                        }
                                      },
                                      "copy-paste-commands": {
                                        "patterns": [
                                          {
                                            "name": "meta.command.copy.tape",
                                            "match": "\\b(Copy)\\s+",
                                            "captures": {
                                              "1": {
                                                "name": "keyword.control.copy.tape"
                                              }
                                            }
                                          },
                                          {
                                            "name": "meta.command.paste.tape",
                                            "match": "\\b(Paste)\\b",
                                            "captures": {
                                              "1": {
                                                "name": "keyword.control.paste.tape"
                                              }
                                            }
                                          }
                                        ]
                                      },
                                      "env-command": {
                                        "name": "meta.command.env.tape",
                                        "match": "\\b(Env)\\s+(\\w+)\\s+",
                                        "captures": {
                                          "1": {
                                            "name": "keyword.control.env.tape"
                                          },
                                          "2": {
                                            "name": "variable.other.env.tape"
                                          }
                                        }
                                      },
                                      "modifier-command": {
                                        "name": "meta.command.modifier.tape",
                                        "match": "\\b(Ctrl|Alt|Shift)\\+((?:Ctrl|Alt|Shift)\\+)*(\\w+)",
                                        "captures": {
                                          "1": {
                                            "name": "constant.language.modifier.tape"
                                          },
                                          "2": {
                                            "name": "constant.language.modifier.tape"
                                          },
                                          "3": {
                                            "name": "constant.language.key.tape"
                                          }
                                        }
                                      },
                                      "key-command": {
                                        "name": "meta.command.key.tape",
                                        "match": "\\b(Enter|Space|Tab|Backspace|Delete|Insert|Escape|Up|Down|Left|Right|PageUp|PageDown|Home|End)((?:@[0-9.]+(?:ms|s|m))?)?(?:\\s+([0-9]+))?",
                                        "captures": {
                                          "1": {
                                            "name": "constant.language.key.tape"
                                          },
                                          "2": {
                                            "patterns": [
                                              {
                                                "include": "#speed-modifier"
                                              }
                                            ]
                                          },
                                          "3": {
                                            "name": "constant.numeric.repeat.tape"
                                          }
                                        }
                                      },
                                      "string": {
                                        "patterns": [
                                          {
                                            "name": "string.quoted.double.tape",
                                            "begin": "\"",
                                            "end": "\"",
                                            "patterns": [
                                              {
                                                "name": "constant.character.escape.tape",
                                                "match": "\\\\[ntr\\\\\"']"
                                              }
                                            ]
                                          },
                                          {
                                            "name": "string.quoted.single.tape",
                                            "begin": "'",
                                            "end": "'"
                                          },
                                          {
                                            "name": "string.quoted.backtick.tape",
                                            "begin": "`",
                                            "end": "`"
                                          }
                                        ]
                                      },
                                      "regex": {
                                        "name": "string.regexp.tape",
                                        "begin": "/",
                                        "end": "/",
                                        "patterns": [
                                          {
                                            "name": "constant.character.escape.tape",
                                            "match": "\\\\."
                                          }
                                        ]
                                      },
                                      "duration": {
                                        "name": "constant.numeric.duration.tape",
                                        "match": "\\b[0-9.]+(?:ms|s|m)\\b"
                                      },
                                      "number": {
                                        "name": "constant.numeric.tape",
                                        "match": "\\b[0-9.]+\\b"
                                      },
                                      "boolean": {
                                        "name": "constant.language.boolean.tape",
                                        "match": "\\b(true|false)\\b"
                                      },
                                      "speed-modifier": {
                                        "name": "constant.numeric.speed.tape",
                                        "match": "@[0-9.]+(?:ms|s|m)?"
                                      }
                                    }
                                  }

                                  """;
}