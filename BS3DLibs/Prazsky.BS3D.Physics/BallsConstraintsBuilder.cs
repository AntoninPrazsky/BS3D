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
		private static readonly float BALL_RADIUS = 0.5f;
		private static readonly float BALL_MASS = 1f;

		private static readonly float SPECULATIVE_MARGIN = 0.1f; //TODO: Zjistit, co přesně tahle hodnota dělá → optimalizace
		private static readonly float SLEEP_TRESHOLD = 0.01f; //TODO: Vykoušet, co bude dělat změna této hodnoty s výkonem a chováním

		private static readonly SpringSettings SPRING_SETTINGS = new SpringSettings(frequency: 15f, dampingRatio: 1f);

		public static PhysicsBall[] BuildBallsStructure(StaticBall[,,] staticBalls, ref Simulation simulation, BodyReference ceilingReference)
		{
			if (staticBalls == null) throw new NullReferenceException("staticBalls cannot be null");
			if (simulation == null) throw new NullReferenceException("simulation nannot be null");

			//TODO: Validace na velikost staticBalls pole

			int levelSize = staticBalls.GetLength(0);
			int xSize = staticBalls.GetLength(1);
			int zSize = staticBalls.GetLength(2);

			PhysicsBall[,,] physicsBalls = new PhysicsBall[levelSize, xSize, zSize]; //Stejné trojrozměrné pole pro fyzikální kuličky

			#region Vytvoření fyzikální reprezentace pro každou kuličku (bez spojení kuliček)

			Sphere sphere = new Sphere(BALL_RADIUS);
			sphere.ComputeInertia(BALL_MASS, out BodyInertia bodyInertia);

			TypedIndex speheShapeIndex = simulation.Shapes.Add(sphere);

			CollidableDescription collidableDescription = new CollidableDescription(speheShapeIndex, SPECULATIVE_MARGIN);

			BodyActivityDescription bodyActivityDescription = new BodyActivityDescription(SLEEP_TRESHOLD);

			for (byte level = 0; level < levelSize; level++)
			{
				for (byte x = 0; x < xSize; x++)
				{
					for (int z = 0; z < zSize; z++)
					{
						if (staticBalls[x, z, level] != null) //Je tady vůbec nějaká kulička?
						{
							BodyDescription bodyDescription = BodyDescription.CreateDynamic(
								staticBalls[x, z, level].GetPosition(),
								bodyInertia,
								collidableDescription,
								bodyActivityDescription);

							BodyHandle bodyHandle = simulation.Bodies.Add(in bodyDescription);

							BodyReference bodyReference = new BodyReference(bodyHandle, simulation.Bodies);

							PhysicsBall ball = new PhysicsBall
							{
								BallReference = bodyReference,
								Type = staticBalls[x, z, level].Type
							};
							ball.SetEmptyConstraints();

							physicsBalls[x, z, level] = ball;
						}
					}
				}
			}

			#endregion Vytvoření fyzikální reprezentace pro každou kuličku (bez spojení kuliček)

			#region Vytvoření spojení mezi dotýkajícími se kuličkami

			List<PhysicsBall> result = new List<PhysicsBall>();

			for (byte level = 0; level < levelSize; level++)
			{
				for (byte x = 0; x < xSize; x++)
				{
					for (int z = 0; z < zSize; z++)
					{
						if (staticBalls[x, z, level] != null) //Je tady vůbec nějaká kulička?
						{
							PhysicsBall currentPhysicsBall = physicsBalls[x, z, level];
							StaticBall currentStaticBall = staticBalls[x, z, level]; //TODO: Připadá mi, že StaticBall už tady k ničemu nepotřebuju - beru z něj jenom pozici, ale tu můžu vzít už i z PhysicsBall

							if (level == levelSize - 1) //Nejvyšší patro - přichytit pouze ke stropu
							{
								//TODO: Nepotřebuju, aby byly kuličky kinematické, potřebuju, aby se přichytily ke stropu

								ConnectBallToCeiling(currentPhysicsBall, ceilingReference, simulation);

								result.Add(currentPhysicsBall); //REFACTOR: To samé se volá nakonci metody
								continue;
							}

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

									if (upBall.HandlesMiddle.Handle4.Value > 0) //Má kulička vpředu spojení na aktuální kuličku?
									{
										currentPhysicsBall.HandlesMiddle.Handle1 = upBall.HandlesMiddle.Handle4; //Pro aktuální kuličku zaregistruju spojení s jejím předním sousedem
									}
									else //Budu muset vytvořit nové spojení mezi kuličkami
									{
										StaticBall upStaticBall = staticBalls[x, z - 1, level];

										var constraintHandle = ConnectBalls(currentPhysicsBall, upBall, currentStaticBall, upStaticBall, simulation);

										currentPhysicsBall.HandlesMiddle.Handle1 = constraintHandle; //Aktuální kulička má vazbu na kuličku před ní
										upBall.HandlesMiddle.Handle4 = constraintHandle; //Kulička před aktuální kuličkou má vazbu na kuličku za ní
									}
								}
							}

							if (x - 1 >= 0)
							{
								if (staticBalls[x - 1, z, level] != null)
								{
									//Vlevo je kulička
									PhysicsBall leftBall = physicsBalls[x - 1, z, level];

									if (leftBall.HandlesMiddle.Handle3.Value > 0) //Má kulička nalevo spojení na aktuální kuličku?
									{
										currentPhysicsBall.HandlesMiddle.Handle2 = leftBall.HandlesMiddle.Handle3;
									}
									else
									{
										StaticBall leftStaticBall = staticBalls[x - 1, z, level];

										var constraintHandle = ConnectBalls(currentPhysicsBall, leftBall, currentStaticBall, leftStaticBall, simulation);

										currentPhysicsBall.HandlesMiddle.Handle2 = constraintHandle;
										leftBall.HandlesMiddle.Handle3 = constraintHandle;
									}
								}
							}

							if (x + 1 < xSize)
							{
								if (staticBalls[x + 1, z, level] != null)
								{
									//Vpravo je kulička
									PhysicsBall rightBall = physicsBalls[x + 1, z, level];

									if (rightBall.HandlesMiddle.Handle2.Value > 0) //Má kulička napravo spojení na aktuální kuličku?
									{
										currentPhysicsBall.HandlesMiddle.Handle3 = rightBall.HandlesMiddle.Handle2;
									}
									else
									{
										StaticBall rightStaticBall = staticBalls[x + 1, z, level];

										var constraintHandle = ConnectBalls(currentPhysicsBall, rightBall, currentStaticBall, rightStaticBall, simulation);

										currentPhysicsBall.HandlesMiddle.Handle3 = constraintHandle;
										rightBall.HandlesMiddle.Handle2 = constraintHandle;
									}
								}
							}

							if (z + 1 < zSize)
							{
								if (staticBalls[x, z + 1, level] != null)
								{
									//Dole je kulička
									PhysicsBall bottomBall = physicsBalls[x, z + 1, level];

									if (bottomBall.HandlesMiddle.Handle1.Value > 0) //Má kulička dole spojení na aktuální kuličku?
									{
										currentPhysicsBall.HandlesMiddle.Handle4 = bottomBall.HandlesMiddle.Handle1;
									}
									else
									{
										StaticBall bottomStaticBall = staticBalls[x, z + 1, level];

										var constraintHandle = ConnectBalls(currentPhysicsBall, bottomBall, currentStaticBall, bottomStaticBall, simulation);

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
							//if (level + 1 < levelSize)
							//{
							//	if (z - 1 >= 0)
							//	{
							//		if (staticBalls[x, z - 1, level + 1] != null)
							//		{
							//			//Vpravo nahoře je kulička
							//			PhysicsBall upRightBall = physicsBalls[x, z - 1, level + 1];

							//			StaticBall upRightStaticBall = staticBalls[x, z - 1, level + 1];

							//			var constraintHandle = ConnectBalls(currentPhysicsBall, upRightBall, currentStaticBall, upRightStaticBall, simulation);

							//			currentPhysicsBall.HandlesTop.Handle2 = constraintHandle; //Aktuální kulička má vazbu na kuličku v levelu + 1 vpravo nahoře
							//			upRightBall.HandlesBottom.Handle3 = constraintHandle; //Kulička v levelu + 1 vpravo nahoře má vazbu na aktuální kuličku (pro ní ta vlevo dole)
							//		}
							//	}

							//	if (x - 1 >= 0)
							//	{
							//		if (staticBalls[x - 1, z - 1, level + 1] != null)
							//		{
							//			//Vlevo nahoře je kulička
							//			PhysicsBall upLeftBall = physicsBalls[x - 1, z - 1, level + 1];

							//			StaticBall upLeftStaticBall = staticBalls[x - 1, z - 1, level + 1];

							//			var constraintHandle = ConnectBalls(currentPhysicsBall, upLeftBall, currentStaticBall, upLeftStaticBall, simulation);

							//			currentPhysicsBall.HandlesTop.Handle1 = constraintHandle;
							//			upLeftBall.HandlesBottom.Handle4 = constraintHandle;
							//		}
							//	}

							//	if (z - 1 >= 0)
							//	{
							//		if (staticBalls[x, z, level + 1] != null)
							//		{
							//			//Vpravo dole je kulička
							//			PhysicsBall downRightBall = physicsBalls[x, z, level + 1];

							//			StaticBall downRightStaticBall = staticBalls[x, z, level + 1];

							//			var constraintHandle = ConnectBalls(currentPhysicsBall, downRightBall, currentStaticBall, downRightStaticBall, simulation);

							//			currentPhysicsBall.HandlesTop.Handle4 = constraintHandle;
							//			downRightBall.HandlesBottom.Handle1 = constraintHandle;
							//		}
							//	}

							//	if (x - 1 >= 0)
							//	{
							//		if (staticBalls[x - 1, z, level + 1] != null)
							//		{
							//			//Vlevo dole je kulička
							//			PhysicsBall downLeftBall = physicsBalls[x - 1, z, level + 1];

							//			StaticBall downLeftStaticBall = staticBalls[x - 1, z, level + 1];

							//			var constraintHandle = ConnectBalls(currentPhysicsBall, downLeftBall, currentStaticBall, downLeftStaticBall, simulation);

							//			currentPhysicsBall.HandlesTop.Handle3 = constraintHandle;
							//			downLeftBall.HandlesBottom.Handle2 = constraintHandle;
							//		}
							//	}
							//}

							// | x,     z + 1, level + 1 | → Vpravo nahoře
							// | x + 1, z + 1, level + 1 | → Vlevo nahoře
							// | x,     z,     level + 1 | → Vpravo dole (v trojrozměrném poli stejná pozice v levelu + 1)
							// | x + 1, z,     level + 1 | → Vlevo dole

							//Existuje vůbec nějaké patro nad aktuálním patrem?
							if (level + 1 < levelSize)
							{
								if (z + 1 < zSize)
								{
									if (staticBalls[x, z + 1, level + 1] != null)
									{
										//Vpravo nahoře je kulička
										PhysicsBall upRightBall = physicsBalls[x, z + 1, level + 1];

										StaticBall upRightStaticBall = staticBalls[x, z + 1, level + 1];

										var constraintHandle = ConnectBalls(currentPhysicsBall, upRightBall, currentStaticBall, upRightStaticBall, simulation);

										currentPhysicsBall.HandlesTop.Handle2 = constraintHandle; //Aktuální kulička má vazbu na kuličku v levelu + 1 vpravo nahoře
										upRightBall.HandlesBottom.Handle3 = constraintHandle; //Kulička v levelu + 1 vpravo nahoře má vazbu na aktuální kuličku (pro ní ta vlevo dole)
									}
								}

								if (x + 1 < xSize && z + 1 < zSize)
								{
									if (staticBalls[x + 1, z + 1, level + 1] != null)
									{
										//Vlevo nahoře je kulička
										PhysicsBall upLeftBall = physicsBalls[x + 1, z + 1, level + 1];

										StaticBall upLeftStaticBall = staticBalls[x + 1, z + 1, level + 1];

										var constraintHandle = ConnectBalls(currentPhysicsBall, upLeftBall, currentStaticBall, upLeftStaticBall, simulation);

										currentPhysicsBall.HandlesTop.Handle1 = constraintHandle;
										upLeftBall.HandlesBottom.Handle4 = constraintHandle;
									}
								}

								if (staticBalls[x, z, level + 1] != null)
								{
									//Vpravo dole je kulička
									PhysicsBall downRightBall = physicsBalls[x, z, level + 1];

									StaticBall downRightStaticBall = staticBalls[x, z, level + 1];

									var constraintHandle = ConnectBalls(currentPhysicsBall, downRightBall, currentStaticBall, downRightStaticBall, simulation);

									currentPhysicsBall.HandlesTop.Handle4 = constraintHandle;
									downRightBall.HandlesBottom.Handle1 = constraintHandle;
								}

								if (x + 1 < xSize)
								{
									if (staticBalls[x + 1, z, level + 1] != null)
									{
										//Vlevo dole je kulička
										PhysicsBall downLeftBall = physicsBalls[x + 1, z, level + 1];

										StaticBall downLeftStaticBall = staticBalls[x + 1, z, level + 1];

										var constraintHandle = ConnectBalls(currentPhysicsBall, downLeftBall, currentStaticBall, downLeftStaticBall, simulation);

										currentPhysicsBall.HandlesTop.Handle3 = constraintHandle;
										downLeftBall.HandlesBottom.Handle2 = constraintHandle;
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

		private static ConstraintHandle ConnectBalls(PhysicsBall physicsBallA, PhysicsBall physicsBallB, StaticBall staticBallA, StaticBall staticBallB, Simulation simulation)
		{
			Vector3 offsetAB = GetLocalOffset(staticBallA.GetPosition(), staticBallB.GetPosition());
			Vector3 offsetBA = Vector3.Negate(offsetAB); //Mohl bych znovu použít GetLocalOffset s otočenými parametry, ale stačí změnit polaritu prvního vektoru

			BallSocket ballSocket = new BallSocket() { LocalOffsetA = offsetAB, LocalOffsetB = offsetBA, SpringSettings = SPRING_SETTINGS };

			return simulation.Solver.Add(physicsBallA.BallReference.Handle, physicsBallB.BallReference.Handle, ballSocket);
		}

		private static ConstraintHandle ConnectBallToCeiling(PhysicsBall physicsBall, BodyReference ceilingReference, Simulation simulation)
		{
			//TODO: Každá koule se musí přilepit ke stropu kolmo, ne vůči jeho středu!
			Vector3 offsetAB = GetLocalOffset(physicsBall.BallReference.Pose.Position, ceilingReference.Pose.Position);
			Vector3 offsetBA = Vector3.Negate(offsetAB);

			BallSocket ballSocket = new BallSocket()
			{
				LocalOffsetA = offsetAB,
				LocalOffsetB = offsetBA,
				SpringSettings = SPRING_SETTINGS
			};

			return simulation.Solver.Add(physicsBall.BallReference.Handle, ceilingReference.Handle, ballSocket);
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