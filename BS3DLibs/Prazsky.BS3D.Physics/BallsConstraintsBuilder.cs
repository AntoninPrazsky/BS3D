using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using Prazsky.BS3D.GameStructure;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Prazsky.BS3D.Physics
{
	public static class BallsConstraintsBuilder
	{
		private static float BALL_RADIUS = 0.5f;
		private static float BALL_MASS = 1f;

		private static float SPECULATIVE_MARGIN = 0.1f; //TODO: Zjistit, co přesně tahle hodnota dělá → optimalizace
		private static float SLEEP_TRESHOLD = 0.01f; //TODO: Vykoušet, co bude dělat změna této hodnoty s výkonem a chováním

		private static readonly SpringSettings SPRING_SETTINGS = new SpringSettings(frequency: 15f, dampingRatio: 1f);

		public static PhysicsBall[] BuildBallsStructure(StaticBall[,,] staticBalls, ref Simulation simulation)
		{
			if (staticBalls == null) throw new NullReferenceException("staticBalls cannot be null");
			if (simulation == null) throw new NullReferenceException("simulation nannot be null");

			//TODO: Validace na velikost staticBalls pole

			PhysicsBall[,,] physicsBalls = new PhysicsBall[staticBalls.GetLength(0), staticBalls.GetLength(1), staticBalls.GetLength(2)]; //Stejné trojrozměrné pole pro fyzikální kuličky

			#region Vytvoření fyzikální reprezentace pro každou kuličku (bez spojení kuliček)

			Sphere sphere = new Sphere(BALL_RADIUS);
			sphere.ComputeInertia(BALL_MASS, out var bodyInertia);

			//INFO: Možná se bude muset dělat pro každou kuličku, jenom test, jestli to projde
			CollidableDescription collidableDescription = new CollidableDescription(simulation.Shapes.Add(sphere), SPECULATIVE_MARGIN);

			BodyActivityDescription bodyActivityDescription = new BodyActivityDescription(SLEEP_TRESHOLD);

			for (byte level = 0; level < staticBalls.GetLength(0); level++)
			{
				for (byte x = 0; x < staticBalls.GetLength(1); x++)
				{
					for (int z = 0; z < staticBalls.GetLength(2); z++)
					{
						if (staticBalls[x, z, level] != null) //Je tady vůbec nějaká kulička?
						{
							BodyDescription bodyDescription = BodyDescription.CreateDynamic(staticBalls[x, z, level].GetPosition(), bodyInertia, collidableDescription, bodyActivityDescription);
							int addedBallReference = simulation.Bodies.Add(bodyDescription);
							BodyReference bodyReference = new BodyReference(addedBallReference, simulation.Bodies);

							PhysicsBall ball = new PhysicsBall { BallReference = bodyReference };
							ball.SetEmptyConstraints();

							physicsBalls[x, z, level] = ball;
						}
					}
				}
			}

			#endregion Vytvoření fyzikální reprezentace pro každou kuličku (bez spojení kuliček)

			#region Vytvoření spojení mezi dotýkajícími se kuličkami

			List<PhysicsBall> result = new List<PhysicsBall>();

			for (byte level = 0; level < staticBalls.GetLength(2); level++)
			{
				for (byte x = 0; x < staticBalls.GetLength(0); x++)
				{
					for (int z = 0; z < staticBalls.GetLength(1); z++)
					{
						if (staticBalls[x, z, level] != null) //Je tady vůbec nějaká kulička?
						{
							PhysicsBall currentPhysicsBall = physicsBalls[x, z, level];
							StaticBall currentStaticBall = staticBalls[x, z, level];

							#region Spojení na sousedící kuličky ve stejném patře

							//   ○
							// ○ ○ ○ (aktuální kulička je ta uprostřed)
							//   ○

							// | x,     z - 1 | → Vpředu
							// | x - 1, z     | → Vlevo
							// | x + 1, z     | → Vpravo
							// | x,     z + 1 | → Vzadu

							if (z - 1 >= 0)
							{
								if (staticBalls[x, z - 1, level] != null)
								{
									//Vpředu je kulička
									PhysicsBall upBall = physicsBalls[x, z - 1, level];

									if (upBall.HandlesMiddle.Handle4 >= 0) //Má kulička vpředu spojení na aktuální kuličku?
									{
										currentPhysicsBall.HandlesMiddle.Handle1 = upBall.HandlesMiddle.Handle4; //Pro aktuální kuličku zaregistruju spojení s jejím předním sousedem
									}
									else //Budu muset vytvořit nové spojení mezi kuličkami
									{
										StaticBall upStaticBall = staticBalls[x, z - 1, level];

										int constraintHandle = ConnectBalls(ref currentPhysicsBall, ref upBall, currentStaticBall, upStaticBall, simulation);

										currentPhysicsBall.HandlesMiddle.Handle1 = constraintHandle; //Aktuální kulička má vazbu na kuličku před ní
										upBall.HandlesMiddle.Handle4 = constraintHandle; //Kulička před aktuální kuličkou má vazbu nad kuličku za ní
									}
								}
							}

							if (x - 1 >= 0)
							{
								if (staticBalls[x - 1, z, level] != null)
								{
									//Vlevo je kulička
									PhysicsBall leftBall = physicsBalls[x - 1, z, level];

									if (leftBall.HandlesMiddle.Handle3 >= 0) //Má kulička nalevo spojení na aktuální kuličku?
									{
										currentPhysicsBall.HandlesMiddle.Handle2 = leftBall.HandlesMiddle.Handle3;
									}
									else
									{
										StaticBall leftStaticBall = staticBalls[x - 1, z, level];

										int constraintHandle = ConnectBalls(ref currentPhysicsBall, ref leftBall, currentStaticBall, leftStaticBall, simulation);

										currentPhysicsBall.HandlesMiddle.Handle2 = constraintHandle;
										leftBall.HandlesMiddle.Handle3 = constraintHandle;
									}
								}
							}

							if (x + 1 <= staticBalls.GetLength(0))
							{
								if (staticBalls[x + 1, z, level] != null)
								{
									//Vpravo je kulička
									PhysicsBall rightBall = physicsBalls[x + 1, z, level];

									if (rightBall.HandlesMiddle.Handle2 >= 0) //Má kulička napravo spojení na aktuální kuličku?
									{
										currentPhysicsBall.HandlesMiddle.Handle3 = rightBall.HandlesMiddle.Handle2;
									}
									else
									{
										StaticBall rightStaticBall = staticBalls[x + 1, z, level];

										int constraintHandle = ConnectBalls(ref currentPhysicsBall, ref rightBall, currentStaticBall, rightStaticBall, simulation);

										currentPhysicsBall.HandlesMiddle.Handle3 = constraintHandle;
										rightBall.HandlesMiddle.Handle2 = constraintHandle;
									}
								}
							}

							if (z + 1 <= staticBalls.GetLength(1))
							{
								if (staticBalls[x, z + 1, level] != null)
								{
									//Dole je kulička
									PhysicsBall bottomBall = physicsBalls[x, z + 1, level];

									if (bottomBall.HandlesMiddle.Handle1 >= 0) //Má kulička dole spojení na aktuální kuličku?
									{
										currentPhysicsBall.HandlesMiddle.Handle4 = bottomBall.HandlesMiddle.Handle1;
									}
									else
									{
										StaticBall bottomStaticBall = staticBalls[x, z + 1, level];

										int constraintHandle = ConnectBalls(ref currentPhysicsBall, ref bottomBall, currentStaticBall, bottomStaticBall, simulation);

										currentPhysicsBall.HandlesMiddle.Handle4 = constraintHandle;
										bottomBall.HandlesMiddle.Handle1 = constraintHandle;
									}
								}
							}

							#endregion Spojení na sousedící kuličky ve stejném patře

							#region Spojení na dotýkající se kuličky v patře nad aktuální kuličkou

							//  ○ ○
							//  ○ ○  (aktuální kulička je ve spodním patře uprostřed těchto čtyř)

							//	V levelu + 1 mají kuličky posunutou reálnou pozici x + 0.5 (→), z + 0.5 (↓) - viz BallsMap.PutBallAt()
							//  Aktuální kuličce tedy v levelu + 1 "odpovídá" kulička "vpravo dole" (v trojrozměrném poli je ale "přímo nad ní")
							//  Dotýkající se kuličky v levelu + 1 k aktuální kuličce potom hledám relativně k této kuličce, co je "vpravo dole" - od této kuličky jsou souřadnice níže
							//	(Používám 3D model, abych si tohle uvědomil)
							// | x,     z - 1, level + 1 | → Vpravo nahoře
							// | x - 1, z - 1, level + 1 | → Vlevo nahoře
							// | x,     z,     level + 1 | → Vpravo dole (v trojrozměrném poli stejná pozice v levelu + 1)
							// | x - 1, z,     level + 1 | → Vlevo dole

							//Existuje vůbec nějaké patro nad aktuálním patrem?
							if (level + 1 < staticBalls.GetLength(2))
							{
								if (z - 1 >= 0)
								{
									if (staticBalls[x, z - 1, level + 1] != null)
									{
										//Vpravo nahoře je kulička
										PhysicsBall upRightBall = physicsBalls[x, z - 1, level + 1];

										//TODO: Otestovat, jestli se tohle někdy stane
										if (upRightBall.HandlesBottom.Handle3 >= 0) //Má kulička vpravo nahoře spojení na aktuální kuličku? (Aktuální kulička je pro ní ta vlevo dole v levelu - 1)
										{
											currentPhysicsBall.HandlesTop.Handle2 = upRightBall.HandlesBottom.Handle3;
										}
										else //Spojení mezi kuličkami ještě neexistuje, vytvořím ho
										{
											StaticBall upRightStaticBall = staticBalls[x, z - 1, level + 1];

											int constraintHandle = ConnectBalls(ref currentPhysicsBall, ref upRightBall, currentStaticBall, upRightStaticBall, simulation);

											currentPhysicsBall.HandlesTop.Handle2 = constraintHandle; //Aktuální kulička má vazbu na kuličku v levelu + 1 vpravo nahoře
											upRightBall.HandlesBottom.Handle3 = constraintHandle; //Kulička v levelu + 1 vpravo nahoře má vazbu na aktuální kuličku (pro ní ta vlevo dole)
										}
									}
								}

								if (x - 1 >= 0 && z - 1 >= 0)
								{
									if (staticBalls[x - 1, z - 1, level + 1] != null)
									{
										//Vlevo nahoře je kulička
										PhysicsBall upLeftBall = physicsBalls[x - 1, z - 1, level + 1];

										//TODO: Otestovat, jestli se tohle někdy stane
										if (upLeftBall.HandlesBottom.Handle4 >= 0) //Má kulička vlevo nahoře spojení na aktuální kuličku? (Aktuální kulička je pro ní ta vpravo dole v levelu - 1)
										{
											currentPhysicsBall.HandlesTop.Handle1 = upLeftBall.HandlesBottom.Handle4;
										}
										else
										{
											StaticBall upLeftStaticBall = staticBalls[x - 1, z - 1, level + 1];

											int constraintHandle = ConnectBalls(ref currentPhysicsBall, ref upLeftBall, currentStaticBall, upLeftStaticBall, simulation);

											currentPhysicsBall.HandlesTop.Handle1 = constraintHandle;
											upLeftBall.HandlesBottom.Handle4 = constraintHandle;
										}
									}
								}

								//Tady nemusím kontrolovat, že x >= 0 a z >= 0
								if (staticBalls[x, z, level + 1] != null)
								{
									//Vpravo dole je kulička
									PhysicsBall downRightBall = physicsBalls[x, z, level + 1];

									//TODO: Otestovat, jestli se tohle někdy stane
									if (downRightBall.HandlesBottom.Handle1 >= 0) //Má kulička pravo dole spojení na aktuální kuličku? (Aktuální kulička je pro ní ta vlevo nahoře v levelu - 1)
									{
										currentPhysicsBall.HandlesTop.Handle4 = downRightBall.HandlesBottom.Handle1;
									}
									else
									{
										StaticBall downRightStaticBall = staticBalls[x, z, level + 1];

										int constraintHandle = ConnectBalls(ref currentPhysicsBall, ref downRightBall, currentStaticBall, downRightStaticBall, simulation);

										currentPhysicsBall.HandlesTop.Handle4 = constraintHandle;
										downRightBall.HandlesBottom.Handle1 = constraintHandle;
									}
								}

								if (x - 1 >= 0)
								{
									if (staticBalls[x - 1, z, level + 1] != null)
									{
										//Vlevo dole je kulička
										PhysicsBall downLeftBall = physicsBalls[x - 1, z, level + 1];

										//TODO: Otestovat, jestli se tohle někdy stane
										if (downLeftBall.HandlesBottom.Handle2 >= 0) //Má kulička vlevo dole spojení na aktuální kuličku? (Aktuální kulička je pro ní ta vpravo nahoře v levelu - 1)
										{
											currentPhysicsBall.HandlesTop.Handle3 = downLeftBall.HandlesBottom.Handle2;
										}
										else
										{
											StaticBall downLeftStaticBall = staticBalls[x - 1, z, level + 1];

											int constraintHandle = ConnectBalls(ref currentPhysicsBall, ref downLeftBall, currentStaticBall, downLeftStaticBall, simulation);

											currentPhysicsBall.HandlesTop.Handle3 = constraintHandle;
											downLeftBall.HandlesBottom.Handle2 = constraintHandle;
										}
									}
								}
							}

							#endregion Spojení na dotýkající se kuličky v patře nad aktuální kuličkou

							#region Spojení na dotýkající se kuličky v patře pod aktuální kuličkou

							//Nevytvořil jsem už náhodou tato spojení, když jsem řešil spojení na kuličky nad aktuální kuličkou? V levelu + 1 se vytvořila spojení na level - 1

							#endregion Spojení na dotýkající se kuličky v patře pod aktuální kuličkou

							result.Add(currentPhysicsBall);
						}
					}
				}
			}

			#endregion Vytvoření spojení mezi dotýkajícími se kuličkami

			return result.ToArray();
		}

		private static int ConnectBalls(ref PhysicsBall physicsBallA, ref PhysicsBall physicsBallB, StaticBall staticBallA, StaticBall staticBallB, Simulation simulation)
		{
			Vector3 offsetAB = GetLocalOffset(staticBallA.GetPosition(), staticBallB.GetPosition());
			Vector3 offsetBA = Vector3.Negate(offsetAB); //Mohl bych znovu použít GetLocalOffset s otočenými parametry, ale stačí změnit polaritu prvního vektoru

			BallSocket ballSocket = new BallSocket() { LocalOffsetA = offsetAB, LocalOffsetB = offsetBA, SpringSettings = SPRING_SETTINGS };

			return simulation.Solver.Add(physicsBallA.BallReference.Handle, physicsBallB.BallReference.Handle, ballSocket);
		}

		private static Vector3 GetLocalOffset(Vector3 ballAPosition, Vector3 ballBPosition)
		{
			return Vector3.Subtract(ballBPosition, ballAPosition) / 2;
		}
	}
}

//Patro se staví od levého zadního po pravý přední
//1 2 3
//4 5 6
//7 8 9